using System.Collections.Generic;
using System.Linq;

namespace EorzeaEventTracker;

public static class RewardPreferences
{
    public static (int Confirmed, int Total) Progress(IEnumerable<TrackedReward> rewards,
        IReadOnlySet<uint> ignored, IReadOnlyDictionary<uint, RewardProgress> progress)
    {
        var included = rewards.Where(r => !ignored.Contains(r.Id)).ToList();
        return (included.Count(r => progress.TryGetValue(r.Id, out var state) && state.Confirmed), included.Count);
    }

    public static void Ignore(CharacterSettings settings, uint id, bool ignored)
    {
        if (ignored) { settings.IgnoredRewards.Add(id); settings.FavoriteRewards.Remove(id); }
        else settings.IgnoredRewards.Remove(id);
    }

    public static void Favorite(CharacterSettings settings, uint id, bool favorite)
    {
        if (favorite) { settings.FavoriteRewards.Add(id); settings.IgnoredRewards.Remove(id); }
        else settings.FavoriteRewards.Remove(id);
    }
}
