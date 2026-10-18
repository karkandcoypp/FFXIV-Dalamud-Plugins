using System;
using System.Collections.Generic;
using System.Text.Json;
using EorzeaEventTracker;

internal static class DeadlineReminderChecks
{
    public static void Run()
    {
        var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var e = new EventInfo { Id = "deadline", StartsAt = now.AddDays(-5), EndsAt = now.AddDays(3) };
        var definition = new TrackingDefinition { EventId = e.Id, Rewards = new() { new() { Id = 1 }, new() { Id = 2 } } };
        var settings = new Configuration();
        settings.FavoriteRewards.Add(1);
        var progress = new Dictionary<uint, RewardProgress> { [1] = new(RewardState.NotUnlocked), [2] = new(RewardState.NotSeen) };
        DeadlineReminder? Pending(DateTimeOffset time, ulong character = 42, bool ready = true) =>
            DeadlineReminderPolicy.Pending(e, definition, settings.FavoriteRewards, progress, settings.DeadlineRemindersSent, character, ready, time);

        Check(Pending(now.AddTicks(-1)) == null, "no deadline warning before 72 hours");
        var threeDays = Pending(now)!.Value;
        Check(threeDays.Stage == 1 && threeDays.RemainingFavorites == 1, "72-hour boundary warns for missing favorites only");
        Check(Pending(now, ready: false) == null && Pending(now, character: 0) == null, "unloaded and anonymous character data cannot trigger reminders");
        settings.DeadlineRemindersSent[threeDays.Key] = threeDays.Stage;
        settings = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(settings))!;
        Check(Pending(now) == null, "three-day reminder stays suppressed after restart");
        Check(Pending(now, character: 43)?.Stage == 1, "another character has an independent reminder history");
        Check(Pending(now.AddDays(2).AddTicks(-1)) == null, "one-day reminder waits until the exact 24-hour boundary");
        var oneDay = Pending(now.AddDays(2))!.Value;
        Check(oneDay.Stage == 2, "one-day warning follows the earlier three-day warning");
        settings.DeadlineRemindersSent[oneDay.Key] = oneDay.Stage;
        Check(Pending(now.AddDays(2).AddHours(1)) == null, "one-day reminder is not repeated each scan or relog");
        settings.DeadlineRemindersSent.Clear();
        Check(Pending(now.AddDays(2).AddHours(12))?.Stage == 2, "late login selects only the urgent reminder");
        Check(Pending(now.AddDays(3)) == null && Pending(now.AddDays(4)) == null, "ended events never trigger a reminder");
        e.StartsAt = now.AddHours(1);
        Check(Pending(now) == null, "upcoming events do not trigger reminders");
        e.StartsAt = now.AddDays(-5);
        foreach (var state in new[] { RewardState.Owned, RewardState.Unlocked, RewardState.SeenPreviously, RewardState.QuestRewardEarned, RewardState.Unknown })
        {
            progress[1] = new(state);
            Check(Pending(now) == null, "no warning for confirmed or unknown reward state: " + state);
        }
        progress.Remove(1);
        Check(Pending(now) == null, "missing game rows do not produce false warnings");
        progress[1] = new(RewardState.NotSeen);
        settings.FavoriteRewards.Clear();
        Check(Pending(now) == null, "no selected favorites means no reminder");
        settings.FavoriteRewards.Add(1);
        settings.DeadlineRemindersSent[threeDays.Key] = 2;
        e.EndsAt = now.AddDays(2);
        Check(Pending(now)?.Stage == 1, "a changed event deadline receives independent reminder history");
        settings.NotifyFavoriteDeadlines = false;
        Check(!JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(settings))!.NotifyFavoriteDeadlines, "reminder opt-out survives restart");
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
