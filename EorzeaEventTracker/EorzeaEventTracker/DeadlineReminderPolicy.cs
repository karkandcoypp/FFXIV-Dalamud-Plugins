using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EorzeaEventTracker;

public readonly record struct DeadlineReminder(string Key, int Stage, int RemainingFavorites);

public static class DeadlineReminderPolicy
{
    // Stages increase with urgency. A late login produces only the most urgent reminder.
    public static DeadlineReminder? Pending(EventInfo e, TrackingDefinition? definition,
        IReadOnlySet<uint> favorites, IReadOnlyDictionary<uint, RewardProgress> progress,
        IReadOnlyDictionary<string, int> sent, ulong contentId, bool ready, DateTimeOffset now, IReadOnlySet<uint>? ignored = null)
    {
        if (!ready || contentId == 0 || !e.Active(now) || definition == null || !e.EndsAt.HasValue) return null;
        var remaining = e.EndsAt.Value - now;
        var stage = remaining <= TimeSpan.FromDays(1) ? 2 : remaining <= TimeSpan.FromDays(3) ? 1 : 0;
        if (stage == 0) return null;
        var key = contentId.ToString(CultureInfo.InvariantCulture) + ":" + e.Id + ":"
            + e.EndsAt.Value.UtcTicks.ToString(CultureInfo.InvariantCulture);
        if (sent.TryGetValue(key, out var delivered) && delivered >= stage) return null;
        // Unknown or missing game rows are not evidence that a reward is still missing.
        var missing = definition.Rewards.Count(r => favorites.Contains(r.Id) && ignored?.Contains(r.Id) != true
            && progress.TryGetValue(r.Id, out var state) && state.State != RewardState.Unknown && !state.Confirmed);
        return missing == 0 ? null : new DeadlineReminder(key, stage, missing);
    }
}
