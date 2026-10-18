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
        Check(ProgressPolicy.Resolve(false, true, 1, true, true).State == RewardState.Unknown, "unloaded character data never reports obtained rewards");
        Check(ProgressPolicy.Resolve(true, true, 0, false, false).State == RewardState.Unlocked, "registered collection survives item consumption");
        Check(ProgressPolicy.Resolve(true, false, 1, false, false).State == RewardState.Owned, "unconsumed collection item is owned but not registered");
        Check(ProgressPolicy.Resolve(true, null, 0, true, false).State == RewardState.QuestRewardEarned, "guaranteed quest reward remains earned after moving item");
        Check(ProgressPolicy.Resolve(true, null, 0, false, true).State == RewardState.SeenPreviously, "previous observation survives moving or consuming item");
        Check(ProgressPolicy.Resolve(true, null, 0, false, false).State == RewardState.NotSeen, "absent furniture is not falsely marked never owned");
        Check(ProgressPolicy.Resolve(true, false, 0, false, false).State == RewardState.NotUnlocked, "locked collection is distinguished from absent furniture");
        var history = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<uint>>();
        ProgressPolicy.ForCharacter(history, 1).Add(52402);
        Check(!ProgressPolicy.ForCharacter(history, 2).Contains(52402) && ProgressPolicy.ForCharacter(history, 1).Contains(52402), "character reward histories remain isolated");
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
