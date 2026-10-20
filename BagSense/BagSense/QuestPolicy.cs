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
        if (snapshot.ActiveQuests == null || needs.Any(q => q.RequiredCount is null or <= 0 || q.Quality is "unknown" or "hq"))
            return snapshot.Quantity(itemId);
        return (int)Math.Min(int.MaxValue, needs.GroupBy(q => q.QuestId).Sum(g => (long)g.Max(q => q.RequiredCount!.Value)));
    }

    public static int Reservation(InventorySnapshot snapshot, CharacterData data, uint itemId) =>
        (int)Math.Min(int.MaxValue, (long)Required(snapshot, itemId) + CleanupPolicy.Reservation(data, itemId));

    public static int ReservableHq(InventorySnapshot snapshot, uint itemId) => snapshot.Stacks.Where(s => s.ItemId == itemId && s.Hq).Sum(s => s.Quantity);
}
