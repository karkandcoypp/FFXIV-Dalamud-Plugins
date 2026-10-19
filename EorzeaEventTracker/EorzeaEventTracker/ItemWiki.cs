using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EorzeaEventTracker;

public enum WikiState { Found, Missing, Unavailable }
public sealed record WikiResult(WikiState State, string Url = "");

public sealed class ItemWiki : IDisposable
{
    private readonly HttpClient client;
    private readonly Dictionary<string, (DateTimeOffset Until, WikiResult Result)> cache = new(StringComparer.Ordinal);
    public ItemWiki(HttpMessageHandler? handler = null)
    {
        client = handler == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(8);
        client.MaxResponseContentBufferSize = 128 * 1024;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EorzeaEventTracker/0.19 (item wiki lookup)");
    }
    public static bool IsPageUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "ffxiv.consolegameswiki.com"
        && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith("/wiki/", StringComparison.Ordinal)
        && uri.AbsolutePath.Length > 6;

    public async Task<WikiResult> FindAsync(string name, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 500 || name.Contains('|') || name.Any(char.IsControl))
            return new(WikiState.Missing);
        lock (cache) if (cache.TryGetValue(name, out var cached) && cached.Until > DateTimeOffset.UtcNow) return cached.Result;
        try
        {
            var url = "https://ffxiv.consolegameswiki.com/mediawiki/api.php?action=query&format=json&redirects=1&prop=info%7Cpageprops&inprop=url&titles=" + Uri.EscapeDataString(name);
            using var response = await client.GetAsync(url, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(WikiState.Unavailable);
            var json = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            var result = Parse(json);
            lock (cache) cache[name] = (DateTimeOffset.UtcNow.AddMinutes(result.State == WikiState.Found ? 60 : 5), result);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or ObjectDisposedException)
        { return new(WikiState.Unavailable); }
    }

    public static WikiResult Parse(string json)
    {
        try { return ParseCore(json); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        { return new(WikiState.Unavailable); }
    }

    private static WikiResult ParseCore(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (!doc.RootElement.TryGetProperty("query", out var query) || !query.TryGetProperty("pages", out var pages)
            || pages.ValueKind != JsonValueKind.Object) return new(WikiState.Unavailable);
        var entries = pages.EnumerateObject().ToArray();
        if (entries.Length != 1) return new(WikiState.Missing);
        var page = entries[0].Value;
        if (page.TryGetProperty("missing", out _) || page.TryGetProperty("invalid", out _)
            || !page.TryGetProperty("ns", out var ns) || ns.GetInt32() != 0
            || page.TryGetProperty("pageprops", out var props) && props.TryGetProperty("disambiguation", out _)) return new(WikiState.Missing);
        return page.TryGetProperty("fullurl", out var url) && url.ValueKind == JsonValueKind.String && IsPageUrl(url.GetString())
            ? new(WikiState.Found, url.GetString()!) : new(WikiState.Unavailable);
    }
    public void Dispose() => client.Dispose();
}
