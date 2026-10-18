using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaEventTracker;

public sealed record CurrencyBudget(uint ItemId, string Currency, long Required, long? Balance)
{
    public long? Remaining => Balance is { } value ? Math.Max(0, Required - value) : null;
}

public static class FavoriteBudget
{
    // Compatibility with existing event feeds that only supplied the MGF label.
    public static uint CurrencyId(TrackedReward reward) => reward.CurrencyItemId != 0 ? reward.CurrencyItemId
        : reward.Currency.Equals("MGF", StringComparison.OrdinalIgnoreCase) ? 41629u : 0;

    public static List<TrackedReward> Pending(IEnumerable<TrackedReward> rewards, ISet<uint> favorites,
        ISet<uint> ignored, IReadOnlyDictionary<uint, RewardProgress> progress) => rewards
        .Where(r => favorites.Contains(r.Id) && !ignored.Contains(r.Id) && !progress.GetValueOrDefault(r.Id).Confirmed)
        .GroupBy(r => r.Id).Select(g => g.First()).ToList();

    public static List<CurrencyBudget> Calculate(IEnumerable<TrackedReward> pending, IReadOnlyDictionary<uint, long> balances) => pending
        .Where(r => r.Price > 0)
        .GroupBy(r => (Id: CurrencyId(r), Label: CurrencyId(r) == 0 ? r.Currency.Trim().ToUpperInvariant() : ""))
        .Select(g => new CurrencyBudget(g.Key.Id, g.First().Currency, g.Sum(r => (long)r.Price),
            g.Key.Id != 0 && balances.TryGetValue(g.Key.Id, out var balance) ? Math.Max(0, balance) : null))
        .ToList();
}
