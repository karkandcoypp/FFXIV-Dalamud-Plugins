using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EorzeaEventTracker;

// Model-only dependency stub; no game services are used by these checks.
namespace Dalamud.Configuration { public interface IPluginConfiguration { int Version { get; set; } } }

internal static class Program
{
    private static async Task Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "EET-checks-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var cache = Path.Combine(root, "cache.json");
        var bundled = Path.Combine(root, "bundled.json");
        var feed = new EventFeed();
        feed.Events.Add(new EventInfo { Id = "one", Title = "Fixture", StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(1) });
        File.WriteAllText(bundled, JsonSerializer.Serialize(feed));
        File.WriteAllText(cache, "{\"schemaVersion\":1,\"events\":[null]}");
        using var service = new EventFeedService(cache, bundled);
        Check(service.Feed.Events.Count == 1, "malformed cache falls back to bundle");
        File.WriteAllText(cache, "{\"schemaVersion\":1,\"events\":null}");
        await service.RefreshAsync("");
        Check(service.Feed.Events.Count == 1, "null events fall back safely");
        File.WriteAllText(cache, "{\"schemaVersion\":1,\"events\":[]}");
        await service.RefreshAsync("");
        Check(service.Feed.Events.Count == 0, "local refresh reloads cache");
        await service.RefreshAsync("http://example.com/feed");
        Check(service.Status == "Feed URL must be HTTPS", "HTTP is rejected");
        var pending = service.RefreshAsync("https://127.0.0.1:1/feed");
        service.Dispose();
        await pending;
        await service.RefreshAsync("");
        Check(true, "dispose during refresh completes without exception");
        File.Delete(cache); File.Delete(bundled); Directory.Delete(root);
    }
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
