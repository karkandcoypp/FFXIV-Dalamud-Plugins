using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EorzeaEventTracker;

public static class FeedPolicy
{
    public static bool IsImageUrl(string url) => url != null && url.Length <= 2048 && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host is "lds-img.finalfantasyxiv.com" or "img.finalfantasyxiv.com";

    public static IReadOnlyDictionary<string, TrackingDefinition> Catalog(EventFeed feed,
        IReadOnlyDictionary<string, TrackingDefinition> bundled)
    {
        var result = new Dictionary<string, TrackingDefinition>();
        foreach (var e in feed.Events)
        {
            if (e.Tracking != null) result[e.Id] = e.Tracking;
            else if (bundled.TryGetValue(e.Id, out var old)) result[e.Id] = old;
        }
        return result;
    }

    public static void Validate(EventFeed? feed)
    {
        if (feed == null || feed.SchemaVersion != 1 || feed.Events == null || feed.Events.Count > 500)
            throw new IOException("Unsupported feed");
        var ids = new HashSet<string>();
        var quests = 0;
        var rewards = 0;
        foreach (var e in feed.Events)
        {
            if (e == null || !Text(e.Id, 128) || !Text(e.Title, 500) || !ids.Add(e.Id)
                || e.StartsAt == null || e.EndsAt == null || e.StartsAt >= e.EndsAt
                || !List(e.Requirements) || !List(e.Steps) || !List(e.Rewards) || !List(e.Notes)
                || e.Description == null || e.Description.Length > 10000 || e.ImageUrl == null
                || (e.ImageUrl.Length > 0 && !IsImageUrl(e.ImageUrl))) throw new IOException("Malformed event");
            if (e.Translations == null || e.Translations.Count > 10) throw new IOException("Invalid translations");
            foreach (var language in e.Translations)
                if (!Text(language.Key, 16) || language.Value == null || language.Value.Count > 500
                    || language.Value.Any(t => !Text(t.Key, 10000) || !Text(t.Value, 10000)))
                    throw new IOException("Invalid translations");
            if (e.Tracking is not { } tracking) continue;
            if (tracking.EventId != e.Id || tracking.Quests == null || tracking.Rewards == null
                || tracking.Quests.Count > 64 || tracking.Rewards.Count > 200
                || tracking.Quests.Any(q => q == null || q.Id < 65536 || q.Id > 131071 || !Text(q.Name, 500))
                || tracking.Rewards.Any(r => r == null || r.Id == 0 || r.Id > 1000000 || r.Icon > 1000000
                    || r.Price < 0 || !Text(r.Currency, 64) || !Text(r.Name, 500))
                || tracking.Quests.Select(q => q.Id).Distinct().Count() != tracking.Quests.Count
                || tracking.Rewards.Select(r => r.Id).Distinct().Count() != tracking.Rewards.Count
                || tracking.Rewards.Any(r => r.EarnedByQuest != 0 && !tracking.Quests.Any(q => q.Id == r.EarnedByQuest)))
                throw new IOException("Invalid tracking definition");
            quests += tracking.Quests.Count;
            rewards += tracking.Rewards.Count;
            if (quests > 256 || rewards > 2000) throw new IOException("Tracking catalog too large");
            // Remote feeds may contain IDs and text, never a local asset path or executable logic.
            tracking.Asset = "";
        }
    }

    private static bool Text(string? text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max;
    private static bool List(List<string>? list) => list != null && list.Count <= 200 && list.All(s => Text(s, 10000));
}
