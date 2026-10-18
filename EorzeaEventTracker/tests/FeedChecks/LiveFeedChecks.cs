using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EorzeaEventTracker;

internal static class LiveFeedChecks
{
    public static async Task Run(string url, string? reportPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "EET-live-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var bundle = Path.Combine(root, "empty-bundle.json");
        var cache = Path.Combine(root, "cache.json");
        File.WriteAllText(bundle, "{\"schemaVersion\":1,\"events\":[]}");
        var locale = new Localizer(new Configuration { Language = "ru" });
        using var service = new EventFeedService(cache, bundle);
        Check(service.Feed.Events.Count == 0, "empty bundled catalog and no previous cache");
        await service.RefreshAsync(url);
        Check(service.Status.StartsWith("Updated ", StringComparison.Ordinal), "real HTTPS feed loaded: " + service.Status);
        var e = service.Feed.Events.Single();
        var catalog = FeedPolicy.Catalog(service.Feed, new Dictionary<string, TrackingDefinition>());
        Check(catalog.TryGetValue(e.Id, out var definition), "tracking uses only the downloaded definition");
        Check(e.Active(DateTimeOffset.UtcNow) || e.Upcoming(DateTimeOffset.UtcNow), "event is visible under the real current dates");
        Check(!e.Verified, "automatic event retains the partial-details marker");
        Check(e.Translations.TryGetValue("ru", out var russian) && russian.TryGetValue(e.Title, out _), "Russian title is supplied online");
        var russianTitle = locale.Text(e.Title, e);
        Check(Regex.IsMatch(russianTitle, "[А-Яа-яЁё]"), "Russian title resolves through the actual Localizer");
        var rewards = definition!.Rewards;
        Check(rewards.Count > 0 && rewards.All(r => r.Id > 0 && r.Icon > 0), "online rewards have item IDs and icons");
        var priced = rewards.Where(r => r.Price > 0).ToList();
        Check(priced.Count > 0 && priced.All(r => r.CurrencyItemId > 0), "online prices have explicit currency IDs");
        var mapped = rewards.Where(r => r.LocationId.Length > 0).ToList();
        Check(mapped.Count > 0 && mapped.All(r => e.Locations.Any(l => l.Id == r.LocationId)), "reward map buttons resolve to downloaded NPC locations");
        Check(definition.Quests.Any(q => q.LocationId.Length > 0 && e.Locations.Any(l => l.Id == q.LocationId)), "quest map buttons resolve to downloaded NPC locations");
        Check(e.Locations.All(l => l.MapId > 0 && l.TerritoryId > 0 && float.IsFinite(l.X) && float.IsFinite(l.Y)), "map destinations pass the real client feed validation");
        var favorites = rewards.Select(r => r.Id).ToHashSet();
        var progress = new Dictionary<uint, RewardProgress>();
        var pending = FavoriteBudget.Pending(rewards, favorites, new HashSet<uint>(), progress);
        // Only arithmetic uses a simulated balance; live game memory is intentionally not claimed here.
        var balances = priced.Select(r => r.CurrencyItemId).Distinct().ToDictionary(id => id, _ => 100L);
        var budgets = FavoriteBudget.Calculate(pending, balances);
        Check(budgets.Sum(b => b.Required) == priced.Sum(r => (long)r.Price), "actual budget code sums downloaded favorite prices and excludes unknown prices");
        Check(budgets.All(b => b.Remaining == Math.Max(0, b.Required - 100)), "deficit subtracts the explicitly simulated balance");
        progress[priced[0].Id] = new(RewardState.ManuallyObtained);
        var reduced = FavoriteBudget.Calculate(FavoriteBudget.Pending(rewards, favorites, new HashSet<uint>(), progress), balances);
        Check(reduced.Sum(b => b.Required) == budgets.Sum(b => b.Required) - priced[0].Price, "manual obtained mark removes an online reward from farming cost");
        using var images = new EventImages(Path.Combine(root, "images"));
        string? image = null;
        for (var i = 0; i < 110 && image == null; i++)
        {
            image = images.GetPath(e.ImageUrl);
            if (image == null) await Task.Delay(200);
        }
        Check(image != null && EventImages.ValidImage(File.ReadAllBytes(image)), "actual artwork downloader fetched and validated the official cover");
        using var offline = new EventFeedService(cache, bundle);
        var saved = offline.Feed.Events.Single();
        Check(saved.Id == e.Id && saved.Locations.Count == e.Locations.Count && locale.Text(saved.Title, saved) == russianTitle,
            "feed cache preserves Russian text, tracking and map destinations across offline restart");
        var report = new
        {
            Url = url, CheckedAt = DateTimeOffset.UtcNow, EventId = e.Id, Title = russianTitle,
            Rewards = rewards.Count, PricedRewards = priced.Count, UnknownPrices = rewards.Count(r => r.Price == 0 && r.EarnedByQuest == 0),
            RussianRewards = rewards.Count(r => locale.Text(r.Name, e) != r.Name),
            RussianQuests = definition.Quests.Count(q => locale.Text(q.Name, e) != q.Name),
            Locations = e.Locations, RewardsWithMapButtons = mapped.Count,
            QuestsWithMapButtons = definition.Quests.Count(q => q.LocationId.Length > 0),
            BudgetsWithSimulatedBalance100 = budgets, CoverDownloaded = true, OfflineRestartVerified = true,
            NoBundledFallback = true, GameWindowRenderingVerified = false, GameMapClickVerified = false,
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (reportPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, json);
        }
        Console.WriteLine(json);
        Console.WriteLine("NOT VERIFIED HERE: game rendering, live character currency balance and the in-game map click require FFXIV/Dalamud.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
