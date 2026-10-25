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
