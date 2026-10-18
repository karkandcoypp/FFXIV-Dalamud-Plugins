using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EorzeaEventTracker;

public sealed class EventImages : IDisposable
{
    private readonly string directory;
    private readonly HttpClient client;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim downloads = new(2, 2);
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> requests = new();
    private volatile bool disposed;
    public EventImages(string directory, HttpMessageHandler? handler = null)
    {
        this.directory = directory;
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    }

    // Called by the UI: only returns ready files, never waits for the network.
    public string? GetPath(string url)
    {
        if (disposed || !FeedPolicy.IsImageUrl(url)) return null;
        var task = requests.GetOrAdd(url, u => new Lazy<Task<string?>>(() => Download(u))).Value;
        return task.IsCompletedSuccessfully ? task.Result : null;
    }

    public void RetryFailed()
    {
        foreach (var pair in requests)
            if (pair.Value.IsValueCreated && pair.Value.Value.IsCompletedSuccessfully && pair.Value.Value.Result == null)
                requests.TryRemove(pair.Key, out _);
    }

    private async Task<string?> Download(string url)
    {
        var entered = false;
        var token = lifetime.Token;
        var file = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".img");
        try
        {
            await downloads.WaitAsync(token).ConfigureAwait(false);
            entered = true;
            if (File.Exists(file))
            {
                var cached = await File.ReadAllBytesAsync(file, token).ConfigureAwait(false);
                if (ValidImage(cached)) return file;
            }
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is not { } uri || !FeedPolicy.IsImageUrl(uri.AbsoluteUri)) return null;
            if (response.Content.Headers.ContentLength > 4_000_000) return null;
            using var buffer = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + count > 4_000_000) return null;
                buffer.Write(chunk, 0, count);
            }
            var data = buffer.ToArray();
            if (!ValidImage(data)) return null;
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(file + ".tmp", data, token).ConfigureAwait(false);
            File.Move(file + ".tmp", file, true);
            return file;
        }
        catch (Exception) { return null; } // Missing artwork never prevents opening the journal.
        finally { if (entered) downloads.Release(); }
    }

    public static bool ValidImage(byte[] data)
    {
        if (data.Length is < 24 or > 4_000_000) return false;
        if (data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && data.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            return Dimensions(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4)),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4)));
        if (data[0] != 0xff || data[1] != 0xd8) return false;
        for (var offset = 2; offset + 4 < data.Length;)
        {
            if (data[offset++] != 0xff) return false;
            while (offset < data.Length && data[offset] == 0xff) offset++;
            if (offset + 2 >= data.Length) return false;
            var marker = data[offset++];
            if (marker is 0xd9 or 0xda) return false;
            if (marker is 0x01 or >= 0xd0 and <= 0xd8) continue;
            var length = (data[offset] << 8) | data[offset + 1];
            if (length < 2 || offset + length > data.Length) return false;
            if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
                return length >= 8 && Dimensions((uint)((data[offset + 5] << 8) | data[offset + 6]),
                    (uint)((data[offset + 3] << 8) | data[offset + 4]));
            offset += length;
        }
        return false;
    }

    private static bool Dimensions(uint width, uint height) => width is > 0 and <= 8192
        && height is > 0 and <= 8192 && (ulong)width * height <= 16_000_000;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        client.Dispose();
    }
}
