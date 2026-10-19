using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EorzeaEventTracker;

internal static class FeedHealthChecks
{
    public static async Task Run()
    {
        var now = DateTimeOffset.UtcNow;
        var e = new EventInfo { Id = "health", Title = "Health", Verified = true, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1),
            Validation = new() { Status = "passed", CheckedAt = now } };
        var feed = new EventFeed { GeneratedAt = now, Events = [e] };
        var online = new FeedLoadInfo(FeedOrigin.Remote, false, now, now);
        Check(FeedHealth.Confidence(e, feed, online, now) == DataConfidence.Confirmed, "review plus recent relationship proof confirms data");
        e.Verified = false;
        Check(FeedHealth.Confidence(e, feed, online, now) == DataConfidence.Partial, "successful links alone never promote automatic coverage to reviewed");
        e.Verified = true;
        Check(FeedHealth.Confidence(e, feed, online with { Fallback = true }, now) == DataConfidence.Cached, "failed refresh reports cached data despite prior success");
        e.Validation.CheckedAt = now.AddHours(-49);
        Check(FeedHealth.Confidence(e, feed, online, now) == DataConfidence.Stale, "old relationship proof is stale even on a fresh download");
        e.Validation.CheckedAt = now.AddDays(1);
        Check(FeedHealth.Confidence(e, feed, online, now) == DataConfidence.Stale, "future proof is not trusted");
        e.Validation.CheckedAt = now;
        feed.GeneratedAt = now.AddHours(-49);
        Check(FeedHealth.Confidence(e, feed, online, now) == DataConfidence.Stale, "old publication cannot appear fresh because it was downloaded again");
        feed.GeneratedAt = now;
        var report = FeedHealth.Report("test", "https://user:secret@example.com/feed?token=private#secret", feed,
            online with { Error = "Failed https://user:secret@example.com/feed?key=private in C:\\Users\\Private\\cache.json" });
        Check(!report.Contains("secret") && !report.Contains("private", StringComparison.OrdinalIgnoreCase)
            && report.Contains("lastSuccessfulLoad"), "copied diagnostics remove URL credentials, parameters and local paths");
        var root = Path.Combine(Path.GetTempPath(), "EET-health-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var bundle = Path.Combine(root, "bundle.json");
            var cache = Path.Combine(root, "cache.json");
            File.WriteAllText(bundle, JsonSerializer.Serialize(feed));
            var handler = new Handler { Data = JsonSerializer.SerializeToUtf8Bytes(feed) };
            DateTimeOffset? success;
            using (var service = new EventFeedService(cache, bundle, handler))
            {
                Check(service.LoadInfo.LastSuccess == null && service.LoadInfo.Origin == FeedOrigin.Bundled, "bundle does not invent a successful online timestamp");
                await service.RefreshAsync("https://example.com/feed?token=private");
                success = service.LoadInfo.LastSuccess;
                Check(success != null && !service.LoadInfo.Fallback && service.LoadInfo.Error == null, "valid load records a successful download");
                handler.Status = HttpStatusCode.ServiceUnavailable;
                await service.RefreshAsync("https://example.com/feed");
                Check(service.LoadInfo.LastSuccess == success && service.LoadInfo.Fallback && service.LoadInfo.Error!.Contains("503"), "HTTP error preserves last success and reports cause");
                handler.Status = HttpStatusCode.OK;
                handler.Data = "invalid json"u8.ToArray();
                await service.RefreshAsync("https://example.com/feed");
                Check(service.LoadInfo.LastSuccess == success && service.LoadInfo.Error!.Contains("JsonException"), "invalid response cannot overwrite a successful timestamp");
                handler.Data = JsonSerializer.SerializeToUtf8Bytes(feed);
                await service.RefreshAsync("https://example.com/feed");
                success = service.LoadInfo.LastSuccess;
                Check(service.LoadInfo.Error == null && !service.LoadInfo.Fallback, "successful retry clears previous error");
            }
            using (var offline = new EventFeedService(cache, bundle))
                Check(offline.LoadInfo.Origin == FeedOrigin.Cache && offline.LoadInfo.LastSuccess == success, "verified cache receipt restores successful load time after restart");
            File.AppendAllText(cache, " ");
            using (var modified = new EventFeedService(cache, bundle))
                Check(modified.LoadInfo.LastSuccess == null, "receipt from different cache bytes cannot assert a download time");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public byte[] Data = [];
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(Status) { RequestMessage = request, Content = new ByteArrayContent(Data) });
    }
}
