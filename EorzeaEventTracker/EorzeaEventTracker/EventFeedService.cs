using System;
using System.IO;
using System.Net.Http;
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
    public string Status { get; private set; } = "Not loaded";
    public DateTimeOffset? UpdatedAt { get; private set; }

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
        foreach (var path in new[] { cachePath, bundledPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                if (new FileInfo(path).Length > 2_000_000) throw new IOException("Feed too large");
                var parsed = JsonSerializer.Deserialize<EventFeed>(File.ReadAllText(path), Options);
                Validate(parsed);
                Feed = parsed!;
                Status = "Loaded " + Path.GetFileName(path);
                return;
            }
            catch (Exception ex) { Status = "Local feed error: " + ex.Message; }
        }
        Status = "No valid local event feed";
    }

    public async Task RefreshAsync(string feedUrl)
    {
        if (disposed) return;
        if (string.IsNullOrWhiteSpace(feedUrl)) { LoadLocal(); return; }
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        { Status = "Feed URL must be HTTPS"; return; }
        if (!await gate.WaitAsync(0)) return;
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
            Feed = feed!;
            UpdatedAt = DateTimeOffset.Now;
            Status = "Updated " + UpdatedAt.Value.ToString("g");
        }
        catch (Exception ex) { Status = "Offline: using cached feed (" + ex.Message + ")"; }
        finally { gate.Release(); }
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
