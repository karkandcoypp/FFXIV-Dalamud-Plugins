using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace BagSense;

public sealed partial class ItemCatalog
{
    private readonly Dictionary<uint, List<ItemSource>> sources = new();
    private readonly Dictionary<uint, List<RecipeLink>> recipeLinks = new();
    private readonly Dictionary<uint, List<ExchangeUse>> acquisitionTrades = new();
    public Dictionary<uint, TradeCost[]> RecipeIngredients { get; } = new();
    public Dictionary<uint, QuestGate> QuestGates { get; } = new();
    private void Source(uint id, ItemSource source)
    {
        if (id == 0) return;
        if (!sources.TryGetValue(id, out var list)) sources[id] = list = new();
        if (!list.Contains(source)) list.Add(source);
    }
    private static string Role(Item item)
    {
        string category = item.ItemUICategory.Value.Name.ToString();
        if (category == "Unobtainable") return "Legacy";
        if (category == "Outfits") return "Outfit";
        if (category == "Gardening") return "Gardening";
        if (category == "Seasonal Miscellany") return "Event";
        if (category == "Part") return "Component";
        if (category is "Tabletop" or "Painting" or "Wall-mounted" or "Table" or "Exterior Wall" or "Interior Wall" or "Flooring" or "Ceiling Light"
            or "Rug" or "Construction Permit" or "Roof" or "Fence" or "Window" or "Door" or "Roof Decoration" or "Exterior Wall Decoration"
            or "Placard" or "Wall") return "Housing";
        string description = item.Description.ToString();
        string name = item.Name.ToString();
        if (description.Contains("Exchangeable for gil.", StringComparison.Ordinal)) return "GilToken";
        if (name.Contains("Dark Matter", StringComparison.Ordinal)) return "Repair";
        if (name.Contains("Glamour Prism", StringComparison.Ordinal) || name == "Glamour Dispeller") return "Glamour";
        if (category == "Other" && item.RowId < 100 || item.ItemSearchCategory.IsValid && item.ItemSearchCategory.Value.Name.ToString() == "Currency") return "Currency";
        if (description.Contains("can be exchanged", StringComparison.OrdinalIgnoreCase) || description.Contains("currency", StringComparison.OrdinalIgnoreCase)) return "Token";
        if (item.EquipSlotCategory.RowId != 0) return "Equipment";
        if (category.Contains("Meal") || category.Contains("Food")) return "Food";
        if (category.Contains("Medicine")) return "Medicine";
        if (category.Contains("Materia")) return "Materia";
        if (category.Contains("Crystal")) return "Crystal";
        if (category.Contains("Fishing Tackle") || category.Contains("Fishing Bait")) return "Bait";
        if (category.Contains("Dye")) return "Dye";
        if (category.Contains("Furnishing") || category.Contains("Outdoor")) return "Housing";
        if (category.Contains("Repair")) return "Repair";
        if (category.Contains("Treasure")) return "Treasure";
        if (category.Contains("Ingredient") || category.Contains("Metal") || category.Contains("Lumber") || category.Contains("Stone")
            || category.Contains("Cloth") || category.Contains("Leather") || category.Contains("Reagent") || category.Contains("Bone") || category.Contains("Seafood")) return "Material";
        if (item.ItemAction.RowId != 0) return "Usable";
        return "Unresolved";
    }
    private void IndexKnowledge(ExcelModule excel)
    {
        foreach (var recipe in excel.GetSheet<Recipe>(Language.English))
        {
            if (!recipe.ItemResult.IsValid || recipe.ItemResult.Value.Name.IsEmpty) continue;
            string job = recipe.CraftType.Value.Name.ToString();
            int level = recipe.RecipeLevelTable.Value.ClassJobLevel;
            RecipeIngredients[recipe.RowId] = recipe.Ingredient.Select((r, i) => (Ref: r, Count: recipe.AmountIngredient[i]))
                .Where(p => p.Ref.RowId != 0 && p.Count > 0).Select(p => new TradeCost(p.Ref.RowId,
                    items.TryGetRow(p.Ref.RowId, out var ingredient) ? ingredient.Name.ToString() : "Unresolved ingredient", (uint)p.Count)).ToArray();
            Source(recipe.ItemResult.RowId, new("Craft", job, recipe.RowId, $"Lv {level} · ×{recipe.AmountResult}"));
            for (int i = 0; i < recipe.Ingredient.Count; i++)
            {
                uint id = recipe.Ingredient[i].RowId;
                if (id == 0 || recipe.AmountIngredient[i] == 0) continue;
                if (!recipeLinks.TryGetValue(id, out var list)) recipeLinks[id] = list = new();
                list.Add(new(recipe.RowId, recipe.ItemResult.RowId, recipe.ItemResult.Value.Name.ToString(), recipe.AmountIngredient[i], recipe.AmountResult, job, level));
            }
        }
        var gatheringItems = excel.GetSheet<GatheringItem>(Language.English);
        foreach (var point in excel.GetSheet<GatheringPoint>(Language.English))
        {
            if (!point.GatheringPointBase.IsValid || !point.PlaceName.IsValid) continue;
            var node = point.GatheringPointBase.Value;
            foreach (var entry in node.Item)
                if (gatheringItems.TryGetRow(entry.RowId, out var gathered) && gathered.Item.RowId != 0)
                    Source(gathered.Item.RowId, new("Gather", point.PlaceName.Value.Name.ToString(), point.RowId,
                        $"{node.GatheringType.Value.Name} · Lv {node.GatheringLevel}"));
        }
        foreach (var spot in excel.GetSheet<FishingSpot>(Language.English))
            foreach (var fish in spot.Item.Where(i => i.RowId != 0))
                Source(fish.RowId, new("Fish", spot.PlaceName.IsValid ? spot.PlaceName.Value.Name.ToString() : "", spot.RowId, $"Lv {spot.GatheringLevel}"));
        foreach (var fish in excel.GetSheet<SpearfishingItem>(Language.English))
            if (fish.Item.RowId != 0 && fish.TerritoryType.IsValid && fish.TerritoryType.Value.PlaceName.IsValid) Source(fish.Item.RowId, new("Spear", fish.TerritoryType.Value.PlaceName.Value.Name.ToString(), fish.RowId));
        var jobs = excel.GetSheet<ClassJob>(Language.English).Where(j => !j.Abbreviation.IsEmpty).ToArray();
        var properties = typeof(ClassJobCategory).GetProperties().Where(p => p.PropertyType == typeof(bool)).ToDictionary(p => p.Name);
        foreach (var quest in excel.GetSheet<Quest>(Language.English))
        {
            if (quest.Name.IsEmpty || quest.JournalGenre.RowId == 0) continue;
            uint[] allowed = quest.ClassJobCategory0.RowId is 0 or 1 ? [] : jobs.Where(j => properties.TryGetValue(j.Abbreviation.ToString(), out var p)
                && p.GetValue(quest.ClassJobCategory0.Value) is true).Select(j => j.RowId).ToArray();
            var previous = quest.PreviousQuest.Where(q => q.RowId != 0).Select(q => new RouteRequirement("quest", q.RowId,
                q.IsValid ? q.Value.Name.ToString() : $"Quest #{q.RowId}")).ToArray();
            var extra = new List<string>();
            if (quest.QuestLock.Any(q => q.RowId != 0)) extra.Add("questlock");
            if (quest.InstanceContent.Any(q => q.RowId != 0)) extra.Add("instance");
            if (quest.ClassJobCategory1.RowId != 0 || quest.ClassJobLevel[1] != 0) extra.Add("secondjob");
            if (quest.GrandCompany.RowId != 0 || quest.GrandCompanyRank.RowId != 0) extra.Add("company");
            if (quest.BeastTribe.RowId != 0) extra.Add("tribe");
            if (quest.SatisfactionNpc.RowId != 0) extra.Add("satisfaction");
            if (quest.IsHouseRequired) extra.Add("housing");
            if (quest.MountRequired.RowId != 0) extra.Add("mount");
            if (quest.ClassJobRequired.RowId != 0 || quest.ClassJobUnlock.RowId != 0) extra.Add("jobunlock");
            if (quest.BellStart != quest.BellEnd || quest.Festival.RowId != 0 || quest.RepeatIntervalType != 0) extra.Add("time");
            if (quest.LevelMax != 0) extra.Add("levelmax");
            if (quest.Unknown7 != 0 || previous.Length > 0 && quest.PreviousQuestJoin is not (1 or 2)) extra.Add("script");
            QuestGates[quest.RowId] = new(quest.RowId, quest.Name.ToString(), quest.ClassJobLevel[0], allowed, previous, quest.PreviousQuestJoin, extra.ToArray(), quest.ClassJobCategory0.IsValid ? quest.ClassJobCategory0.Value.Name.ToString() : "");
        }
    }
}
