namespace BagSense;

public static class QuestPolicy
{
    public static bool Relevant(InventorySnapshot snapshot, QuestUse quest) => !quest.Reward
        && (snapshot.ActiveQuests == null ? quest.Repeatable || snapshot.QuestCompleted(quest.QuestId) != true
            : snapshot.ActiveQuests.ContainsKey(quest.QuestId));

    public static int Required(InventorySnapshot snapshot, uint itemId)
    {
        if (!snapshot.Items.TryGetValue(itemId, out var item)) return 0;
        var needs = item.QuestUses.Where(q => Relevant(snapshot, q)).ToArray();
        if (needs.Length == 0) return 0;
        // A missing journal or an uncertain quantity/quality must never free a possibly needed stack.
        if (snapshot.ActiveQuests == null || needs.Any(q => Amount(snapshot, q) == null))
            return snapshot.Quantity(itemId);
        return (int)Math.Min(int.MaxValue, needs.GroupBy(q => q.QuestId).Sum(g => (long)g.Max(q => Amount(snapshot, q)!.Value)));
    }

    // Zero means a known alternative job branch, null means uncertainty/HQ protection.
    public static int? Amount(InventorySnapshot snapshot, QuestUse quest)
    {
        if (quest.RequirementVariants is { Length: > 0 } variants && snapshot.QuestJobs?.TryGetValue(quest.QuestId, out byte job) == true && job != 0)
        {
            var applicable = variants.Where(v => v.Jobs == null || v.Jobs.Length == 0 || v.Jobs.Contains(job)).ToArray();
            if (applicable.Length == 0) return 0;
            if (applicable.Any(v => v.Hq)) return null;
            return applicable.Max(v => v.Count);
        }
        return quest.RequiredCount is null or <= 0 || quest.Quality is "unknown" or "hq" ? null : quest.RequiredCount.Value;
    }

    public static int Reservation(InventorySnapshot snapshot, CharacterData data, uint itemId) =>
        (int)Math.Min(int.MaxValue, (long)Required(snapshot, itemId) + CleanupPolicy.Reservation(data, itemId));

    public static int ReservableHq(InventorySnapshot snapshot, uint itemId) => snapshot.Stacks.Where(s => s.ItemId == itemId && s.Hq).Sum(s => s.Quantity);
}
