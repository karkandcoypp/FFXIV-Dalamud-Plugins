namespace BagSense;

public static class RewardPolicy
{
    public static string Kind(uint action, bool equipment, string category, string name) => action switch
    {
        1322 => "Mount", 853 => "Minion", 25183 => "Music", 3357 => "Card", 29459 => "Portrait",
        20086 => "Accessory", 1013 => "Barding", 37312 => "Glasses",
        2633 when name.StartsWith("Modern Aesthetics", StringComparison.Ordinal) => "Hairstyle",
        2633 when name.StartsWith("Ballroom Etiquette", StringComparison.Ordinal) => "Emote",
        2633 => "Unlock", 43141 => "Record", 19743 => "FieldNote",
        _ when equipment => "Equipment",
        _ when category.Contains("Dye", StringComparison.OrdinalIgnoreCase) => "Dye",
        _ when category.Contains("Furnishing", StringComparison.OrdinalIgnoreCase) || category.Contains("Outdoor", StringComparison.OrdinalIgnoreCase) => "Housing",
        _ when category.Contains("Ingredient", StringComparison.OrdinalIgnoreCase) || category.Contains("Metal", StringComparison.OrdinalIgnoreCase)
            || category.Contains("Lumber", StringComparison.OrdinalIgnoreCase) || category.Contains("Stone", StringComparison.OrdinalIgnoreCase)
            || category.Contains("Cloth", StringComparison.OrdinalIgnoreCase) || category.Contains("Reagent", StringComparison.OrdinalIgnoreCase) => "Material",
        _ => "Item"
    };

    public static bool RedundantDuplicate(InventorySnapshot snapshot, Advice advice) => advice.CanPlan && advice.Available > 0
        && snapshot.Items.TryGetValue(advice.ItemId, out var item) && item.Unlocked == true
        && item.Reward?.Kind is "Mount" or "Minion" or "Music" or "Hairstyle" or "Emote"
        && item.ExchangeUses.Length == 0 && item.RecipeUses.Length == 0 && item.DeliveryUses.Length == 0
        && !item.QuestUses.Any(q => !q.Reward && (q.Repeatable || snapshot.QuestCompleted(q.QuestId) != true));

    public static bool? Unlocked(InventorySnapshot snapshot, uint id) => snapshot.Items.GetValueOrDefault(id)?.Unlocked ?? snapshot.RewardUnlocks?.GetValueOrDefault(id);
}
