using System;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using EorzeaEventTracker;

internal static class CharacterSettingsChecks
{
    public static void Run()
    {
        var settings = new Configuration { Language = "ru", UiScale = 1.2f, OpenOnLogin = true };
        settings.HiddenEvents.Add("legacy-event");
        settings.FavoriteRewards.Add(11);
        settings.IgnoredRewards.Add(12);
        ProgressPolicy.ForCharacter(settings.ManualRewards, 101).Add(1001);
        ProgressPolicy.ForCharacter(settings.ObservedRewards, 101).Add(1002);
        var first = settings.ForCharacter(101);
        var second = settings.ForCharacter(202);
        Check(first.HiddenEvents.Count == 0 && second.HiddenEvents.Count == 0,
            "legacy global hidden events are never assigned to an unknown character");
        Check(settings.HiddenEvents.Contains("legacy-event") && settings.FavoriteRewards.Contains(11) && settings.IgnoredRewards.Contains(12),
            "legacy selections remain archived for recovery");
        Check(first.Language == "ru" && second.Language == "ru" && first.UiScale == 1.2f && second.UiScale == 1.2f,
            "new profiles retain familiar initial display defaults without sharing state");
        first.HiddenEvents.Add("fallguys");
        var now = DateTimeOffset.UtcNow;
        var live = new EventInfo { Id = "live", Verified = true, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1) };
        first.SetEventHidden(live, true);
        var catalog = new Dictionary<string, TrackingDefinition>();
        var quests = new Dictionary<uint, bool>();
        bool Opens(CharacterSettings profile) => EventCompletionPolicy.ShouldAutoOpen(
            new EventFeed { Events = new[] { live }.Where(e => !profile.IsEventHidden(e)).ToList() }, catalog, quests, true, true, now);
        Check(!Opens(first) && Opens(second), "a hidden active event suppresses only its owner's login opening, not another character's");
        RewardPreferences.Favorite(first, 21, true);
        RewardPreferences.Ignore(first, 22, true);
        first.UiScale = 1.6f;
        first.BackgroundOpacity = .7f;
        first.Language = "en";
        first.OpenOnLogin = false;
        first.ShowUnverified = false;
        first.NotifyFavoriteDeadlines = false;
        first.ShowCompactWidget = true;
        first.RemainingOnly = true;
        first.FavoritesOnly = true;
        Check(!second.HiddenEvents.Contains("fallguys") && second.FavoriteRewards.Count == 0 && second.IgnoredRewards.Count == 0,
            "hiding and reward choices on one character cannot affect another server's character");
        Check(second.UiScale == 1.2f && second.Language == "ru" && second.BackgroundOpacity == 1
            && second.OpenOnLogin && second.ShowUnverified && second.NotifyFavoriteDeadlines && !second.ShowCompactWidget
            && !second.RemainingOnly && !second.FavoritesOnly,
            "appearance, language, login, reminders, widget and filters are independent per character");
        Check(ReferenceEquals(settings.ForCharacter(101), first) && settings.ForCharacter(101).HiddenEvents.Contains("fallguys"),
            "switching back restores the original character's choices");
        settings = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(settings))!;
        first = settings.ForCharacter(101);
        second = settings.ForCharacter(202);
        Check(first.HiddenEvents.Contains("fallguys") && first.FavoriteRewards.Contains(21) && first.IgnoredRewards.Contains(22)
            && first.UiScale == 1.6f && first.RemainingOnly && first.FavoritesOnly && first.ShowCompactWidget,
            "character preferences survive save and restart");
        Check(second.HiddenEvents.Count == 0 && second.UiScale == 1.2f && second.Language == "ru",
            "another profile remains untouched after save and restart");
        Check(ProgressPolicy.ForCharacter(settings.ManualRewards, 101).Contains(1001)
            && ProgressPolicy.ForCharacter(settings.ObservedRewards, 101).Contains(1002)
            && !ProgressPolicy.ForCharacter(settings.ManualRewards, 202).Contains(1001)
            && !ProgressPolicy.ForCharacter(settings.ObservedRewards, 202).Contains(1002),
            "existing manual marks and automatic evidence remain attached to their original character");
        var legacy = JsonSerializer.Deserialize<Configuration>("{\"Version\":3,\"Language\":\"ru\",\"HiddenEvents\":[\"old-hidden\"],\"ManualRewards\":{\"101\":[1001]}}")!;
        Check(legacy.ForCharacter(202).HiddenEvents.Count == 0 && legacy.ForCharacter(202).Language == "ru"
            && legacy.ManualRewards["101"].Contains(1001), "a pre-profile configuration loads without sharing hidden events or losing manual history");
        var anonymousRejected = false;
        try { settings.ForCharacter(0); } catch (ArgumentOutOfRangeException) { anonymousRejected = true; }
        Check(anonymousRejected, "anonymous login state cannot create a persistent character profile");
    }
    private static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text);
        Console.WriteLine("PASS: " + text);
    }
}
