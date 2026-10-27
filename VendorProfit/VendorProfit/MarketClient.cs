using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace VendorProfit;

public sealed class MarketCooldownException(string message) : Exception(message);

public sealed record ScanResult(uint World, DateTimeOffset Finished, Suggestion[] Suggestions,
    int Checked, int Unknown, int Failed, string Status, string Scope = "", WorldScanStatus[]? Servers = null);
public sealed record WorldScanStatus(uint World, string Name, int Checked, int Unknown, int Failed);

public sealed class MarketClient : IDisposable
{
    private readonly HttpClient http;
    public MarketClient() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(25) }) { }
    public MarketClient(HttpClient client)
    {
        http = client;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VendorProfit/0.1.2 (+https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins)");
    }
    public async Task<JsonDocument> Get(string path, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var response = await http.GetAsync("https://universalis.app/api/v2/" + path, token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt >= 2)
                    throw new MarketCooldownException("Universalis продолжает ограничивать запросы; проверка остановлена.");
                if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 2)
                {
                    var delay = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(2 << attempt);
                    // Stop rather than retrying earlier than a long server-directed cooldown.
                    if (delay > TimeSpan.FromSeconds(60)) throw new MarketCooldownException("Universalis просит паузу более 60 секунд. Повторите проверку позже.");
                    await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, token).ConfigureAwait(false);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            }
            catch (Exception e) when ((e is HttpRequestException || e is TaskCanceledException && !token.IsCancellationRequested) && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 << attempt), token).ConfigureAwait(false);
            }
        }
    }
    internal static bool UInt(JsonElement e, string key, out uint value)
    {
        value = 0;
        return e.TryGetProperty(key, out var p) && p.TryGetUInt32(out value);
    }
    public static DateTimeOffset? Timestamp(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var p) || !p.TryGetInt64(out var value) || value <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(value); } catch (ArgumentOutOfRangeException) { return null; }
    }
    public static bool Potential(JsonElement result, VendorItem item)
    {
        foreach (var quality in new[] { "nq", "hq" })
        {
            if (quality == "hq" && !item.CanHq) continue;
            if (result.TryGetProperty(quality, out var q) && q.TryGetProperty("minListing", out var m) &&
                m.TryGetProperty("world", out var w) && w.TryGetProperty("price", out var p) &&
                p.TryGetDecimal(out var price) && price > 0 && price < Policy.NpcPrice(item.Npc, quality == "hq")) return true;
        }
        return false;
    }
    public static Suggestion[] ParseBook(JsonElement book, VendorItem item, int maxAge, DateTimeOffset now)
    {
        if (!UInt(book, "itemID", out var id) || id != item.Id ||
            Timestamp(book, "lastUploadTime") is not { } updated || !Policy.Fresh(updated, now, maxAge) ||
            !book.TryGetProperty("listings", out var listings) || listings.ValueKind != JsonValueKind.Array) return [];
        var output = new List<Suggestion>();
        // Partial books still prove individual returned lots; they never prove absence or market totals.
        foreach (var l in listings.EnumerateArray())
        {
            if (!UInt(l, "pricePerUnit", out var price) || !UInt(l, "quantity", out var quantity) ||
                !l.TryGetProperty("hq", out var hq) || hq.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !l.TryGetProperty("onMannequin", out var mannequin) || mannequin.ValueKind != JsonValueKind.False) continue;
            bool estimated = !UInt(l, "tax", out var tax);
            long subtotal = (long)price * quantity;
            if (estimated)
            {
                if (subtotal / 20 > uint.MaxValue) continue;
                tax = (uint)(subtotal / 20);
            }
            if (l.TryGetProperty("total", out var total) && (!total.TryGetInt64(out var t) || t != subtotal)) continue;
            var lot = new Lot(0, id, l.TryGetProperty("retainerName", out var name) ? name.GetString() ?? "" : "", price, quantity, tax, hq.GetBoolean());
            // Equipment condition is unavailable from Universalis; only live offers can validate it.
            if (!item.Equipment && Policy.Evaluate(item, lot) is { Profit: > 0 } quote)
                output.Add(new(item, quote, updated, estimated));
        }
        return output.ToArray();
    }
    public async Task<ScanResult> Scan(uint world, VendorItem[] catalog, int maxAge, Action<string> progress, CancellationToken token, bool quick = false)
    {
        var candidates = new List<VendorItem>();
        int failed = 0, unknown = 0, checkedItems = 0;
        int consecutiveFailures = 0;
        if (quick) candidates.AddRange(catalog.Take(100));
        foreach (var batch in (quick ? Array.Empty<VendorItem>() : catalog).Chunk(100))
        {
            token.ThrowIfCancellationRequested();
            progress($"Проверка каталога: {checkedItems}/{catalog.Length}");
            try
            {
                using var json = await Get($"aggregated/{world}/{string.Join(',', batch.Select(i => i.Id))}", token).ConfigureAwait(false);
                var seen = new HashSet<uint>();
                if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                    throw new JsonException("Missing aggregate results");
                foreach (var r in results.EnumerateArray())
                {
                    if (!UInt(r, "itemId", out var id) || !seen.Add(id)) continue;
                    var item = batch.FirstOrDefault(i => i.Id == id);
                    if (item != null && Potential(r, item)) candidates.Add(item);
                }
                unknown += batch.Count(i => !seen.Contains(i.Id));
                consecutiveFailures = 0;
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !token.IsCancellationRequested) { failed += batch.Length; consecutiveFailures++; }
            checkedItems += batch.Length;
            if (consecutiveFailures >= 3) { unknown += catalog.Length - checkedItems; break; }
            await Task.Delay(300, token).ConfigureAwait(false);
        }
        var suggestions = new List<Suggestion>();
        // Bounded extra traffic. Any unexamined candidates are explicitly counted as unknown.
        unknown += Math.Max(0, candidates.Count - 300);
        foreach (var batch in candidates.DistinctBy(i => i.Id).Take(300).Chunk(20))
        {
            progress($"{(quick ? "Быстрая проверка" : "Полный поиск")}: лотов {suggestions.Count}, предметов {checkedItems}/{candidates.Count}");
            try
            {
                using var json = await Get($"{world}/{string.Join(',', batch.Select(i => i.Id))}?listings=100&entries=0", token).ConfigureAwait(false);
                var root = json.RootElement;
                foreach (var item in batch)
                {
                    JsonElement book;
                    if (root.TryGetProperty("items", out var books))
                    {
                        if (!books.TryGetProperty(item.Id.ToString(), out book)) { unknown++; continue; }
                    }
                    else book = root;
                    if (!UInt(book, "itemID", out var id) || id != item.Id ||
                        (UInt(book, "worldID", out var bookWorld) && bookWorld != world) ||
                        Timestamp(book, "lastUploadTime") is not { } timestamp || !Policy.Fresh(timestamp, DateTimeOffset.UtcNow, maxAge)) { unknown++; continue; }
                    if (!book.TryGetProperty("listings", out var offers) || offers.ValueKind != JsonValueKind.Array) { unknown++; continue; }
                    if (offers.GetArrayLength() >= 100) unknown++; // capped: do not claim exhaustive coverage
                    suggestions.AddRange(ParseBook(book, item, maxAge, DateTimeOffset.UtcNow));
                }
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !token.IsCancellationRequested) { failed += batch.Length; }
            if (quick) checkedItems += batch.Length;
            await Task.Delay(300, token).ConfigureAwait(false);
        }
        return new(world, DateTimeOffset.UtcNow, suggestions.Select(s => s with { World = world }).OrderByDescending(s => s.Quote.Profit).ToArray(), checkedItems, unknown, failed,
            (quick ? "Быстрый список, не весь рынок. " : "Полный каталог. ") + "Universalis: предварительные лоты; проверьте выдачу Market Board.");
    }
    public async Task<ScanResult> ScanChaos(uint anchorWorld, VendorItem[] catalog, int maxAge, Action<string> progress, CancellationToken token)
    {
        // Resolve membership from the service; never infer a listing's world from the cheapest DC aggregate.
        using var centers = await Get("data-centers", token).ConfigureAwait(false);
        var chaos = centers.RootElement.EnumerateArray().Where(d => d.GetProperty("name").GetString() == "Chaos").ToArray();
        if (chaos.Length != 1) throw new JsonException("Chaos отсутствует или неоднозначен в списке дата-центров.");
        var ids = chaos[0].GetProperty("worlds").EnumerateArray().Select(w => w.GetUInt32()).Distinct().ToArray();
        if (ids.Length == 0 || ids.Length > 32 || ids.Contains(0u)) throw new JsonException("Некорректный список серверов Chaos.");
        using var worlds = await Get("worlds", token).ConfigureAwait(false);
        var names = worlds.RootElement.EnumerateArray().ToDictionary(w => w.GetProperty("id").GetUInt32(), w => w.GetProperty("name").GetString() ?? "");
        if (ids.Any(id => !names.TryGetValue(id, out var name) || string.IsNullOrWhiteSpace(name))) throw new JsonException("Не удалось определить имена всех серверов Chaos.");
        var suggestions = new List<Suggestion>();
        var stats = new List<WorldScanStatus>();
        bool cooldown = false;
        var items = catalog.Take(100).ToArray();
        foreach (uint id in ids)
        {
            token.ThrowIfCancellationRequested();
            if (cooldown) { stats.Add(new(id, names[id], 0, items.Length, 0)); continue; }
            progress($"Chaos: {names[id]} ({stats.Count + 1}/{ids.Length})");
            try
            {
                var snapshot = await Scan(id, items, maxAge, p => progress($"Chaos {names[id]} ({stats.Count + 1}/{ids.Length}): {p}"), token, quick: true).ConfigureAwait(false);
                suggestions.AddRange(snapshot.Suggestions.Select(s => s with { World = id, WorldName = names[id] }));
                stats.Add(new(id, names[id], snapshot.Checked, snapshot.Unknown, snapshot.Failed));
            }
            catch (MarketCooldownException) { cooldown = true; stats.Add(new(id, names[id], 0, items.Length, 0)); }
            catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !token.IsCancellationRequested)
            { stats.Add(new(id, names[id], items.Length, 0, items.Length)); }
        }
        return new(anchorWorld, DateTimeOffset.UtcNow, suggestions.OrderByDescending(s => s.Quote.Profit).ToArray(),
            stats.Sum(s => s.Checked), stats.Sum(s => s.Unknown), stats.Sum(s => s.Failed),
            $"Chaos: {ids.Length} серверов, до {items.Length} предметов на сервер. " +
            (cooldown ? "Проверка неполная: сервис запросил длительную паузу. " : "") +
            "Кэш Universalis: наличие и итог покупки проверьте на указанном сервере.", "Chaos", stats.ToArray());
    }
    public void Dispose() => http.Dispose();
}
