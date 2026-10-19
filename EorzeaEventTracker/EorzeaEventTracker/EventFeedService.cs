using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EorzeaEventTracker;

public sealed class EventFeedService : IDisposable
{
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private volatile bool disposed;
    private readonly string cachePath;
    private readonly string bundledPath;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public EventFeed Feed { get; private set; } = new();
    private FeedLoadInfo loadInfo = new();
    public FeedLoadInfo LoadInfo => Volatile.Read(ref loadInfo);
    public string Status => LoadInfo.Status;
    public DateTimeOffset? UpdatedAt => LoadInfo.LastSuccess;
    private void State(FeedLoadInfo info) => Volatile.Write(ref loadInfo, info);
    private sealed record CacheReceipt(string Hash, DateTimeOffset Success, string Source);

    public EventFeedService(string cachePath, string bundledPath, HttpMessageHandler? handler = null)
    {
        client = handler == null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(12);
        this.cachePath = cachePath;
        this.bundledPath = bundledPath;
        LoadLocal();
    }

    private void LoadLocal()
    {
        string? error = null;
        foreach (var path in new[] { cachePath, bundledPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                if (new FileInfo(path).Length > 2_000_000) throw new IOException("Feed too large");
                var bytes = File.ReadAllBytes(path);
                var parsed = JsonSerializer.Deserialize<EventFeed>(bytes, Options);
                Validate(parsed);
                CacheReceipt? receipt = null;
                if (path == cachePath)
                {
                    try
                    {
                        var receiptPath = cachePath + ".state.json";
                        if (File.Exists(receiptPath) && new FileInfo(receiptPath).Length < 10000)
                        {
                            var candidate = JsonSerializer.Deserialize<CacheReceipt>(File.ReadAllText(receiptPath));
                            if (candidate?.Hash == Convert.ToHexString(SHA256.HashData(bytes)) && candidate.Success <= DateTimeOffset.UtcNow.AddMinutes(10)) receipt = candidate;
                        }
                    }
                    catch { /* A legacy or mismatched receipt must not invent a successful load time. */ }
                }
                Feed = parsed!;
                State(new(path == cachePath ? FeedOrigin.Cache : FeedOrigin.Bundled, true, LoadInfo.LastAttempt,
                    receipt?.Success, receipt?.Source ?? "", error, false, "Loaded " + Path.GetFileName(path)));
                return;
            }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
        }
        State(LoadInfo with { Error = error ?? "No valid local event feed", Fallback = true, Status = "No valid local event feed" });
    }

    public async Task RefreshAsync(string feedUrl)
    {
        if (disposed) return;
        if (string.IsNullOrWhiteSpace(feedUrl)) { LoadLocal(); return; }
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            State(LoadInfo with { LastAttempt = DateTimeOffset.UtcNow, Error = "Feed URL must be HTTPS", Fallback = true, Status = "Feed URL must be HTTPS" });
            return;
        }
        if (!await gate.WaitAsync(0)) return;
        State(LoadInfo with { LastAttempt = DateTimeOffset.UtcNow, Refreshing = true });
        try
        {
            using var result = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
            result.EnsureSuccessStatusCode();
            if (result.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new IOException("Feed redirected outside HTTPS");
            if (result.Content.Headers.ContentLength > 2_000_000) throw new IOException("Feed too large");
            await using var stream = await result.Content.ReadAsStreamAsync(lifetime.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, lifetime.Token)) > 0)
            {
                if (buffer.Length + read > 2_000_000) throw new IOException("Feed too large");
                buffer.Write(chunk, 0, read);
            }
            var data = buffer.ToArray();
            var feed = JsonSerializer.Deserialize<EventFeed>(data, Options);
            Validate(feed);
            var directory = Path.GetDirectoryName(cachePath)!;
            Directory.CreateDirectory(directory);
            var tmp = cachePath + ".tmp";
            await File.WriteAllBytesAsync(tmp, data, lifetime.Token);
            File.Move(tmp, cachePath, true);
            var success = DateTimeOffset.UtcNow;
            var receiptPath = cachePath + ".state.json";
            await File.WriteAllTextAsync(receiptPath + ".tmp", JsonSerializer.Serialize(new CacheReceipt(
                Convert.ToHexString(SHA256.HashData(data)), success, FeedHealth.SafeUrl(feedUrl))), lifetime.Token);
            File.Move(receiptPath + ".tmp", receiptPath, true);
            Feed = feed!;
            State(new(FeedOrigin.Remote, false, LoadInfo.LastAttempt, success, FeedHealth.SafeUrl(feedUrl),
                null, false, "Updated " + success.ToLocalTime().ToString("g")));
        }
        catch (Exception ex)
        {
            State(LoadInfo with { Fallback = true, Error = ex.GetType().Name + ": " + ex.Message,
                Refreshing = false, Status = "Offline: using cached feed (" + ex.Message + ")" });
        }
        finally { State(LoadInfo with { Refreshing = false }); gate.Release(); }
    }

    private static void Validate(EventFeed? feed) => FeedPolicy.Validate(feed);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        client.Dispose();
        // An outstanding refresh still owns the gate and releases it in finally.
    }
}
