using System;
using System.Collections.Generic;
using System.Text.Json;
using EorzeaEventTracker;

internal static class RewardPreferenceChecks
{
    public static void Run()
    {
        var settings = new Configuration();
        var rewards = new List<TrackedReward> { new() { Id = 1 }, new() { Id = 2 } };
        var progress = new Dictionary<uint, RewardProgress> { [1] = new(RewardState.Owned), [2] = new(RewardState.NotSeen) };
        Check(RewardPreferences.Progress(rewards, settings.IgnoredRewards, progress) == (1, 2), "normal reward progress counts all rewards");
        RewardPreferences.Favorite(settings, 2, true);
        RewardPreferences.Ignore(settings, 2, true);
        Check(RewardPreferences.Progress(rewards, settings.IgnoredRewards, progress) == (1, 1), "ignored missing reward is excluded from denominator");
        Check(!settings.FavoriteRewards.Contains(2), "ignoring a favorite removes it from favorites");
        RewardPreferences.Ignore(settings, 1, true);
        Check(RewardPreferences.Progress(rewards, settings.IgnoredRewards, progress) == (0, 0), "ignored obtained rewards are excluded from numerator too");
        settings = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(settings))!;
        Check(settings.IgnoredRewards.SetEquals(new uint[] { 1, 2 }), "ignored reward choices persist after restart");
        RewardPreferences.Ignore(settings, 1, false);
        Check(RewardPreferences.Progress(rewards, settings.IgnoredRewards, progress) == (1, 1) && progress[1].Confirmed, "unignoring restores progress without losing acquisition history");
        RewardPreferences.Favorite(settings, 2, true);
        Check(!settings.IgnoredRewards.Contains(2) && settings.FavoriteRewards.Contains(2), "favoriting restores interest in an excluded reward");
        var e = new EventInfo { Id = "test", StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddHours(12) };
        var definition = new TrackingDefinition { Rewards = rewards };
        settings.IgnoredRewards.Add(2); // Defensive check for inconsistent older/manually edited settings.
        Check(DeadlineReminderPolicy.Pending(e, definition, settings.FavoriteRewards, progress, settings.DeadlineRemindersSent,
            1, true, DateTimeOffset.UtcNow, settings.IgnoredRewards) == null, "ignored favorites do not trigger deadline reminders");
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
