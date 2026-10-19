using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EorzeaEventTracker;

internal static class ItemWikiChecks
{
    private const string Found = """{"query":{"pages":{"1":{"ns":0,"fullurl":"https://ffxiv.consolegameswiki.com/wiki/Gentlebean_Knit_Cap"}}}}""";
    public static async Task Run()
    {
        Check(ItemWiki.Parse(Found).State == WikiState.Found, "existing wiki page is linked");
        Check(ItemWiki.Parse("""{"query":{"pages":{"-1":{"ns":0,"missing":""}}}}""").State == WikiState.Missing, "missing page does not produce a link");
        Check(ItemWiki.Parse("""{"query":{"pages":{"1":{"ns":0,"pageprops":{"disambiguation":""},"fullurl":"https://ffxiv.consolegameswiki.com/wiki/Name"}}}}""").State == WikiState.Missing, "ambiguous page is not opened as an item");
        Check(ItemWiki.Parse(Found.Replace("https://ffxiv.consolegameswiki.com/", "https://evil.test/")).State == WikiState.Unavailable, "wiki response cannot supply an arbitrary browser host");
        foreach (var invalid in new[] { "<html>blocked</html>", "{}", "null", "{\"query\":{\"pages\":{\"1\":{\"ns\":\"bad\"}}}}" })
            Check(ItemWiki.Parse(invalid).State == WikiState.Unavailable, "malformed response stays unavailable");
        Check(!ItemWiki.IsPageUrl("https://ffxiv.consolegameswiki.com.evil.test/wiki/Item")
            && !ItemWiki.IsPageUrl("https://user@ffxiv.consolegameswiki.com/wiki/Item")
            && !ItemWiki.IsPageUrl("http://ffxiv.consolegameswiki.com/wiki/Item"), "page URL allowlist is strict");
        var handler = new Handler();
        using var wiki = new ItemWiki(handler);
        var first = await wiki.FindAsync("Winner's Crown #1");
        Check(first.State == WikiState.Found && handler.Query.Contains("%231"), "item title is encoded, not interpreted as a fragment");
        await wiki.FindAsync("Winner's Crown #1");
        Check(handler.Requests == 1, "repeat clicks use the page cache");
        handler.Status = HttpStatusCode.ServiceUnavailable;
        Check((await wiki.FindAsync("Another item")).State == WikiState.Unavailable, "temporary failures do not imply a missing page");
        handler.Status = HttpStatusCode.OK;
        Check((await wiki.FindAsync("Another item")).State == WikiState.Found, "failed lookup can be retried");
        Check((await wiki.FindAsync("Name|Other")).State == WikiState.Missing, "item cannot inject a multi-title query");
        Console.WriteLine("PASS: item wiki existence, ambiguity, failures, caching and safe URLs");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Handler : HttpMessageHandler
    {
        public int Requests;
        public string Query = "";
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests++;
            Query = request.RequestUri!.Query;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Found) });
        }
    }
}
