using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EorzeaEventTracker;

internal static class RewardDiscoveryChecks
{
    public static void Run()
    {
        Check(RewardCategoryPolicy.Classify(0, 63, 1322) == RewardCategory.Mounts, "mount unlock action, not Other UI category, identifies mount");
        Check(RewardCategoryPolicy.Classify(0, 81, 0) == RewardCategory.Minions, "minions use item category");
        Check(RewardCategoryPolicy.Classify(3, 35, 0) == RewardCategory.Gear, "equippable reward is gear");
        foreach (var category in new uint[] { 57, 65, 73, 75, 76, 77, 78, 79, 80 })
            Check(RewardCategoryPolicy.Classify(0, category, 0) == RewardCategory.Housing, "housing covers furniture, exterior and interior fixtures");
        Check(RewardCategoryPolicy.Classify(0, 55, 0) == RewardCategory.Other && RewardCategoryPolicy.Classify(0, 999, 0) == RewardCategory.Other,
            "dyes and unknown categories remain Other");
        var rewards = new List<TrackedReward> { new() { Id = 1, Name = "Bean Coat" }, new() { Id = 2, Name = "Bean Pet" }, new() { Id = 3, Name = "Chair" } };
        var categories = new Dictionary<uint, RewardCategory> { [1] = RewardCategory.Gear, [2] = RewardCategory.Minions };
        var progress = new Dictionary<uint, RewardProgress> { [2] = new(RewardState.ManuallyObtained) };
        var settings = new CharacterSettings { FavoriteRewards = [2] };
        var selected = RewardListPolicy.Select(rewards, "bean", RewardSortMode.Recommended, settings, progress, false, true, RewardCategory.Minions, categories);
        Check(selected.Count == 1 && selected[0].Id == 2 && progress[2].Confirmed, "type search and favorites intersect without discarding obtained marks");
        Check(RewardListPolicy.Select(rewards, "bean", RewardSortMode.Recommended, settings, progress, true, false, RewardCategory.Minions, categories).Count == 0,
            "remaining-only excludes obtained items before section grouping");
        Check(RewardListPolicy.Select(rewards, "", RewardSortMode.Recommended, settings, progress, false, false, RewardCategory.Other, categories).Single().Id == 3,
            "unclassified reward remains discoverable under Other");
        var e = new EventInfo { Id = "event", StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(1) };
        var config = new Configuration();
        var a = config.ForCharacter(100);
        var b = config.ForCharacter(200);
        Check(EventDiscoveryPolicy.IsNew(a, e), "unviewed occurrence is new");
        Check(EventDiscoveryPolicy.MarkViewed(a, e) && !EventDiscoveryPolicy.MarkViewed(a, e), "view marker is saved once");
        Check(!EventDiscoveryPolicy.IsNew(a, e) && EventDiscoveryPolicy.IsNew(b, e), "another character has its own discovery state");
        e.Title = "Updated translation";
        Check(!EventDiscoveryPolicy.IsNew(a, e), "text updates do not mark an existing occurrence new");
        var key = EventDiscoveryPolicy.Key(e);
        a.ExpandedObtainedEvents.Add(key);
        a.RewardTypeFilter = RewardCategory.Housing;
        var loaded = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        Check(loaded.ForCharacter(100).ExpandedObtainedEvents.Contains(key) && loaded.ForCharacter(100).RewardTypeFilter == RewardCategory.Housing
            && !EventDiscoveryPolicy.IsNew(loaded.ForCharacter(100), e), "section/filter/view choices survive restart");
        Check(loaded.ForCharacter(200).ExpandedObtainedEvents.Count == 0 && loaded.ForCharacter(200).RewardTypeFilter == RewardCategory.All,
            "collapse and category preferences are character-scoped");
        e.StartsAt = e.StartsAt!.Value.AddYears(1); e.EndsAt = e.EndsAt!.Value.AddYears(1);
        Check(EventDiscoveryPolicy.IsNew(a, e) && !a.ExpandedObtainedEvents.Contains(EventDiscoveryPolicy.Key(e)), "recurring event is new and obtained section starts collapsed");
        Console.WriteLine("PASS: reward categories, combined filters, obtained groups and character-scoped event discovery");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
