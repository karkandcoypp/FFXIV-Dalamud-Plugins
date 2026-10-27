using System.Net;
using System.Text;
using System.Text.Json;
using VendorProfit;

int checks = 0;
void Check(bool passed, string name) { if (!passed) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
var demi = new VendorItem(8144, "Clear Demimateria III", 5000, false, 99, false);
var fossil = new VendorItem(1, "Dinosaur Fossil", 59, false, 99, false);
Check(Policy.Evaluate(demi, new(1, 8144, "Bagirka", 4532, 3, 679, false)) is { Cost: 14275, Revenue: 15000, Profit: 725 }, "screenshot: tax once on whole stack");
Check(Policy.Evaluate(demi, new(2, 8144, "Lion-face", 4771, 2, 477, false)) is { Profit: -19 }, "screenshot: misleading unit price is a loss");
Check(Policy.Evaluate(fossil, new(3, 1, "Verre-one", 35, 99, 173, false)) is { Cost: 3638, Profit: 2203 }, "screenshot: fossil 99-stack");
Check(Policy.Evaluate(fossil, new(4, 1, "Verre-one", 35, 34, 59, false)) is { Profit: 757 }, "screenshot: stack tax rounds down");
Check(Policy.NpcPrice(709, true) == 780 && Policy.NpcPrice(31, true) == 35 && Policy.NpcPrice(1, true) == 2, "HQ NPC rounding");
var basic = new Lot(5, 1, "retainer", 1, 1, 0, false);
Check(Policy.Evaluate(fossil, basic with { Quantity = 0 }) == null, "zero quantity excluded");
Check(Policy.Evaluate(fossil, basic with { Quantity = 100 }) == null, "impossible stack excluded");
Check(Policy.Evaluate(fossil, basic with { Price = 0 }) == null, "missing price excluded");
Check(Policy.Evaluate(fossil, basic with { Hq = true }) == null, "impossible HQ excluded");
Check(Policy.Evaluate(fossil, basic with { Mannequin = true }) == null, "mannequin excluded");
Check(Policy.Evaluate(fossil, basic with { Damaged = true }) == null, "damaged equipment excluded");
Check(Policy.Evaluate(fossil, basic with { Item = 7 }) == null, "item mismatch excluded");
Check(Policy.Evaluate(fossil with { Npc = 0 }, basic) == null, "no vendor value excluded");
Check(Policy.Evaluate(fossil with { Npc = uint.MaxValue, Stack = uint.MaxValue }, basic with { Quantity = 1000000, Price = 1000000 })!.Cost == 1_000_000_000_000, "64-bit stack arithmetic");
var a = new Lot(10, 1, "one", 35, 99, 173, false);
var b = new Lot(11, 1, "two", 41, 58, 118, false);
Check(Policy.Match([b, a], ["one", "99", "3638"])?.Id == 10, "sorted UI matched by contents, not index");
Check(Policy.Match([a, a with { Id = 12, Hq = true }], ["one", "99", "3638"]) == null, "ambiguous NQ/HQ fingerprints fail closed");
Check(Policy.Match([a], ["two", "99", "3638"]) == null, "recycled renderer not marked");
Check(Policy.Match([a], ["one", "99", "3465"]) == null, "pre-tax displayed total not trusted");
Check(Policy.NormalizeNumber("3,638 ") == "3638", "localized currency formatting");
var visibleRow = Policy.ParseRow(["35 ", "99", "3,638 ", "Verre-one"]);
Check(visibleRow is { Price: 35, Quantity: 99, Cost: 3638, Retainer: "Verre-one" }, "native row columns parsed in visual order");
Check(Policy.FromRow(fossil, visibleRow!, [a]) is { Profit: 2203 }, "visible row uses native data without packet receipt or search request flags");
Check(Policy.FromRow(fossil, visibleRow!, [a with { Tax = 172 }]) == null, "displayed total must equal current native tax-inclusive amount");
Check(Policy.FromRow(fossil, visibleRow!, [a with { Item = 7 }]) == null, "old item snapshot cannot highlight new item");
Check(Policy.FromRow(fossil, visibleRow!, [a with { Mannequin = true }]) == null, "native mannequin cannot be highlighted");
Check(Policy.ParseRow(["35", "99", "3638", "4", "name"]) == null, "extra numeric columns fail closed");
Check(Policy.ParseRow(["35", "99", "3464", "name"]) == null, "inconsistent rendered total fails closed");
var hqFossil = fossil with { CanHq = true };
Check(Policy.FromRow(hqFossil, visibleRow!, [a, a with { Hq = true }]) is { Lot.Conservative: true, Revenue: 5841 }, "indistinguishable NQ HQ rows use lower vendor value");
var quickCatalog = Enumerable.Range(1, 250).Select(i => new VendorItem((uint)i, "item", 1, false, 99, false)).ToDictionary(i => i.Id);
Check(Policy.QuickItems(quickCatalog, Enumerable.Range(1, 250).Select(i => (uint)i)).Length == 100, "quick search bounded to 100 learned items");
var now = DateTimeOffset.UtcNow;
Check(!Policy.Fresh(now.AddHours(-4), now, 180) && !Policy.Fresh(now.AddHours(4), now, 180), "old and future timestamps excluded");
string Book(string extra = "", long? time = null) => $$"""{"itemID":8144,"lastUploadTime":{{time ?? now.ToUnixTimeMilliseconds()}},"listings":[{"pricePerUnit":4532,"quantity":3,"hq":false,"onMannequin":false{{extra}}}]}""";
Suggestion[] Parse(string value) { using var doc = JsonDocument.Parse(value); return MarketClient.ParseBook(doc.RootElement, demi, 180, now); }
Check(Parse(Book()).Single() is { EstimatedTax: true, Quote.Profit: 725 }, "API missing tax explicitly estimated once");
Check(Parse(Book(",\"tax\":100")).Single() is { EstimatedTax: false, Quote.Cost: 13696 }, "API actual tax overrides estimate");
Check(Parse(Book(",\"total\":1")).Length == 0, "inconsistent API total excluded");
Check(Parse(Book(time: 0)).Length == 0 && Parse(Book(time: long.MaxValue)).Length == 0, "invalid timestamps rejected");
Check(Parse(Book(time: now.AddDays(-1).ToUnixTimeMilliseconds())).Length == 0, "stale book rejected");
Check(Parse(Book().Replace("\"onMannequin\":false", "\"irrelevant\":false")).Length == 0, "missing listing identity fields rejected");
Check(Parse(Book().Replace("\"quantity\":3", "\"quantity\":-3")).Length == 0, "negative API quantities rejected");
using (var doc = JsonDocument.Parse("""{"nq":{"minListing":{"dc":{"price":1}}}}"""))
    Check(!MarketClient.Potential(doc.RootElement, demi), "other-world minimum never used for current world");
var handler = new FakeHandler();
using (var market = new MarketClient(new HttpClient(handler)))
{
    using var response = await market.Get("test", CancellationToken.None);
    Check(handler.Calls == 2, "429 retries with Retry-After");
}
var cooling = new FakeHandler(true);
using (var market = new MarketClient(new HttpClient(cooling)))
{
    try { using var response = await market.Get("test", CancellationToken.None); throw new Exception("Expected cooldown"); }
    catch (MarketCooldownException) { Check(cooling.Calls == 1, "long Retry-After stops instead of premature retry"); }
}
using (var market = new MarketClient(new HttpClient(new PartialHandler(Book(",\"tax\":679")))))
{
    var catalog = new[] { demi }.Concat(Enumerable.Range(1, 100).Select(i => new VendorItem((uint)(10000 + i), "dummy", 1, false, 99, false))).ToArray();
    var result = await market.Scan(80, catalog, 180, _ => { }, CancellationToken.None);
    Check(result.Suggestions.Length == 1 && result.Failed == 1 && result.Unknown == 99, "partial batch failure preserves successful suggestions and diagnostics");
}
var direct = new DirectHandler();
using (var market = new MarketClient(new HttpClient(direct)))
{
    var result = await market.Scan(80, quickCatalog.Values.ToArray(), 180, _ => { }, CancellationToken.None, quick: true);
    Check(direct.Calls == 5 && direct.Aggregates == 0 && result.Checked == 100, "quick mode uses at most five books and zero aggregate requests");
}
var chaosHandler = new ChaosHandler(Book(",\"tax\":679"));
using (var market = new MarketClient(new HttpClient(chaosHandler)))
{
    var result = await market.ScanChaos(80, [demi], 180, _ => { }, CancellationToken.None);
    Check(result.Scope == "Chaos" && result.Servers?.Length == 8 && result.Checked == 8, "Chaos visits all eight discovered worlds");
    Check(result.Suggestions.Length == 7 && result.Servers!.Single(w => w.World == 83).Failed == 1, "one failed world preserves other seven worlds");
    Check(result.Suggestions.Select(s => s.World).Distinct().Count() == 7 && result.Suggestions.All(s => s.WorldName == ChaosHandler.Names[s.World]), "identical items on different worlds retain correct world labels");
    Check(chaosHandler.Calls == 10 && chaosHandler.Aggregates == 0, "Chaos probes use only metadata plus bounded direct books");
    var restored = JsonSerializer.Deserialize<ScanResult>(JsonSerializer.Serialize(result))!;
    Check(restored.Scope == "Chaos" && restored.Servers!.Length == 8 && restored.Suggestions[0].WorldName.Length > 0, "Chaos cache preserves world identities and per-world failures");
}
using (var market = new MarketClient(new HttpClient(new PartialHandler(Book(",\"tax\":679").Replace("\"itemID\":8144", "\"itemID\":8144,\"worldID\":39")))))
{
    var result = await market.Scan(80, [demi], 180, _ => { }, CancellationToken.None, quick: true);
    Check(result.Suggestions.Length == 0 && result.Unknown == 1, "mismatched response world cannot be labelled as requested world");
}
var coolingChaos = new ChaosHandler(Book(",\"tax\":679"), cooldown: true);
using (var market = new MarketClient(new HttpClient(coolingChaos)))
{
    var result = await market.ScanChaos(80, [demi], 180, _ => { }, CancellationToken.None);
    Check(result.Suggestions.Length == 1 && result.Unknown == 7 && coolingChaos.Calls == 4,
        "Chaos stops on server cooldown and preserves earlier worlds");
}
{
    var legacy = JsonSerializer.Deserialize<ScanResult>("{\"World\":80,\"Finished\":\"2026-10-26T00:00:00Z\",\"Suggestions\":[],\"Checked\":1,\"Unknown\":0,\"Failed\":0,\"Status\":\"old\"}")!;
    Check(legacy.World == 80 && legacy.Servers == null && legacy.Scope == "", "previous cache remains readable");
}
if (args.Contains("--chaos-live"))
{
    using var market = new MarketClient();
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var result = await market.ScanChaos(80, [demi], 180, _ => { }, CancellationToken.None);
    Check(result.Servers?.Length == 8 && result.Checked == 8 && result.Failed == 0, "live Chaos metadata and all eight world books");
    Check(result.Suggestions.All(s => s.World != 0 && !string.IsNullOrWhiteSpace(s.WorldName)), "live Chaos lots have explicit world identities");
    Console.WriteLine($"Chaos live probe: {timer.Elapsed.TotalSeconds:F2}s, worlds={result.Servers!.Length}, lots={result.Suggestions.Length}, unknown={result.Unknown}");
}
if (args.Contains("--live"))
{
    using var market = new MarketClient();
    using var aggregate = await market.Get("aggregated/80/8144", CancellationToken.None);
    Check(aggregate.RootElement.GetProperty("results").GetArrayLength() == 1, "live single-item aggregate shape");
    using var book = await market.Get("80/8144?listings=3&entries=0", CancellationToken.None);
    Check(book.RootElement.GetProperty("itemID").GetUInt32() == 8144 && book.RootElement.GetProperty("listings").ValueKind == JsonValueKind.Array,
        "live single book shape");
    using var multi = await market.Get("80/8144,8142?listings=3&entries=0", CancellationToken.None);
    Check(multi.RootElement.GetProperty("items").TryGetProperty("8144", out _), "live multi-item book shape");
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var quick = await market.Scan(80, [demi], 180, _ => { }, CancellationToken.None, quick: true);
    Check(quick.Checked == 1 && quick.Failed == 0, "live quick mode reads books directly");
    Console.WriteLine($"Live quick probe: {timer.Elapsed.TotalSeconds:F2}s, unknown={quick.Unknown}, lots={quick.Suggestions.Length}");
    Console.WriteLine("LIVE HTTP schema checks passed; this does not validate in-game rendering.");
}
Console.WriteLine($"{checks} checks passed");

sealed class FakeHandler(bool longCooldown = false) : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        var result = new HttpResponseMessage(Calls == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
        if (Calls == 1) result.Headers.RetryAfter = new(TimeSpan.FromSeconds(longCooldown ? 120 : 0));
        result.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return Task.FromResult(result);
    }
}

sealed class PartialHandler(string book) : HttpMessageHandler
{
    private int aggregates;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        bool aggregate = request.RequestUri!.AbsolutePath.Contains("aggregated");
        string content = aggregate ? (++aggregates == 1 ? """{"results":[{"itemId":8144,"nq":{"minListing":{"world":{"price":4532}}}}]}""" : "{}") : book;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
    }
}

sealed class DirectHandler : HttpMessageHandler
{
    public int Calls, Aggregates;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++; if (request.RequestUri!.AbsolutePath.Contains("aggregated")) Aggregates++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"items\":{}}", Encoding.UTF8, "application/json") });
    }
}

sealed class ChaosHandler(string book, bool cooldown = false) : HttpMessageHandler
{
    public static readonly Dictionary<uint,string> Names = new() { [80]="Cerberus", [83]="Louisoix", [71]="Moogle", [39]="Omega", [401]="Phantom", [97]="Ragnarok", [400]="Sagittarius", [85]="Spriggan" };
    public int Calls, Aggregates;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        string path = request.RequestUri!.AbsolutePath;
        if (cooldown && path.Contains("/83/"))
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(120));
            return Task.FromResult(response);
        }
        string body;
        if (path.EndsWith("data-centers")) body = "[{\"name\":\"Chaos\",\"worlds\":[80,83,71,39,401,97,400,85]}]";
        else if (path.EndsWith("worlds")) body = JsonSerializer.Serialize(Names.Select(n => new { id=n.Key, name=n.Value }));
        else { if (path.Contains("aggregated")) Aggregates++; body = path.Contains("/83/") ? "{" : book; }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
