using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaEventTracker;

public enum RewardSortMode { Recommended, Name, Price }
public static class RewardListPolicy
{
    public static List<TrackedReward> Select(IEnumerable<TrackedReward> rewards, string search, RewardSortMode sort,
        CharacterSettings preferences, IReadOnlyDictionary<uint, RewardProgress> progress, bool remainingOnly, bool favoritesOnly)
    {
        var terms = (search ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = rewards.Where(r => terms.All(term => r.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            && (!favoritesOnly || preferences.FavoriteRewards.Contains(r.Id))
            && (!remainingOnly || !preferences.IgnoredRewards.Contains(r.Id) && !progress.GetValueOrDefault(r.Id).Confirmed));
        int Group(TrackedReward r) => progress.GetValueOrDefault(r.Id).Confirmed ? 3
            : preferences.IgnoredRewards.Contains(r.Id) ? 2 : preferences.FavoriteRewards.Contains(r.Id) ? 0 : 1;
        var ordered = filtered.OrderBy(Group);
        // LINQ ordering is stable: equal keys retain feed order rather than jumping every scan.
        return sort switch
        {
            RewardSortMode.Name => ordered.ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            RewardSortMode.Price => ordered.ThenBy(FavoriteBudget.CurrencyId)
                .ThenBy(r => FavoriteBudget.CurrencyId(r) == 0 ? r.Currency : "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Price > 0 ? (long)r.Price : long.MaxValue)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => ordered.ToList(),
        };
    }
}
