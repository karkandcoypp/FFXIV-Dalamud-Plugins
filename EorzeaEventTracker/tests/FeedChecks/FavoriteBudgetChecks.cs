using System;
using System.Collections.Generic;
using System.Linq;
using EorzeaEventTracker;

internal static class FavoriteBudgetChecks
{
    public static void Run()
    {
        var rewards = new List<TrackedReward>
        {
            new() { Id = 1, Price = 410 }, new() { Id = 2, Price = 410 },
            new() { Id = 3, Price = 750 }, new() { Id = 4, Price = 100 },
            new() { Id = 5, Price = 50, Currency = "Tokens", CurrencyItemId = 99 },
            new() { Id = 6, Price = 0 }, new() { Id = 7, Price = 0, EarnedByQuest = 71004 },
        };
        var favorites = new HashSet<uint> { 1, 2, 3, 5, 6, 7 };
        var ignored = new HashSet<uint> { 3 };
        var progress = new Dictionary<uint, RewardProgress> { [2] = new(RewardState.ManuallyObtained) };
        var pending = FavoriteBudget.Pending(rewards, favorites, ignored, progress);
        Check(pending.Select(r => r.Id).SequenceEqual(new uint[] { 1, 5, 6, 7 }), "budget excludes obtained, ignored and nonfavorite rewards");
        var balances = new Dictionary<uint, long> { [41629] = 150, [99] = 70 };
        var budget = FavoriteBudget.Calculate(pending, balances);
        Check(budget.Count == 2 && budget[0].Required == 410 && budget[0].Remaining == 260,
            "MGF deficit subtracts character balance from remaining favorite costs");
        Check(budget[1].Required == 50 && budget[1].Remaining == 0, "different currencies stay separate and surplus never becomes negative farming");
        Check(FavoriteBudget.Calculate(pending, new Dictionary<uint, long>()).All(b => b.Balance == null && b.Remaining == null),
            "unavailable balances stay unknown instead of being treated as zero");
        progress[1] = new(RewardState.Unlocked);
        Check(FavoriteBudget.Calculate(FavoriteBudget.Pending(rewards, favorites, ignored, progress), balances).All(b => b.ItemId != 41629),
            "newly obtained rewards immediately disappear from farming cost");
        var large = new[] { new TrackedReward { Id = 10, Price = int.MaxValue }, new TrackedReward { Id = 11, Price = int.MaxValue } };
        Check(FavoriteBudget.Calculate(large, balances)[0].Required == 2L * int.MaxValue, "cost totals use 64-bit arithmetic");
        Check(FavoriteBudget.Pending(new[] { rewards[0], rewards[0] }, favorites, ignored, new Dictionary<uint, RewardProgress>()).Count == 1,
            "a favorite is budgeted once even if repeated in input");
        Check(FavoriteBudget.Calculate(new[] { rewards[5], rewards[6] }, balances).Count == 0,
            "unknown prices and quest rewards are not reported as paid currency goals");
        var unknown = new[] { new TrackedReward { Id = 12, Price = 20, Currency = "New currency" } };
        Check(FavoriteBudget.Calculate(unknown, balances)[0].Remaining == null, "new currencies without an item mapping keep their deficit unknown");
    }
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
