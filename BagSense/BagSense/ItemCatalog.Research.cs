using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace BagSense;
public sealed partial class ItemCatalog
{
    private readonly Dictionary<string, (uint Territory, uint Map)> researchMaps = new(StringComparer.OrdinalIgnoreCase);
    private static string ZoneKey(string zone) => zone.Replace("The Forbidden Land, ", "", StringComparison.OrdinalIgnoreCase).Trim();
    public (uint Territory, uint Map)? ResearchMap(string zone) => researchMaps.TryGetValue(ZoneKey(zone), out var map) ? map : null;
    private readonly Dictionary<uint, List<ResearchUse>> research = new();
    private void Research(uint item, ResearchUse use)
    {
        if (item == 0) return;
        if (!research.TryGetValue(item, out var list)) research[item] = list = new();
        list.Add(use);
    }
    private void IndexMissionResearch(ExcelModule excel)
    {
        foreach (var group in excel.GetSheet<Map>(Language.English).Where(m => m.TerritoryType.RowId != 0 && m.PlaceName.IsValid && m.MapIndex == 0 && !m.IsEvent)
            .GroupBy(m => ZoneKey(m.PlaceName.Value.Name.ToString()), StringComparer.OrdinalIgnoreCase))
        {
            var maps = group.Select(m => (Territory: m.TerritoryType.RowId, Map: m.RowId)).Distinct().ToArray();
            if (maps.Length == 1) researchMaps[group.Key] = maps[0];
        }
        uint unlock = QuestGates.Values.FirstOrDefault(q => q.Name == "A Cosmic Homecoming")?.Id ?? 0;
        foreach (var mission in excel.GetSheet<WKSMissionUnit>(Language.English))
        {
            if (mission.Name.IsEmpty) continue;
            string name = mission.Name.ToString();
            string zone = mission.PlaceName.IsValid ? mission.PlaceName.Value.Name.ToString() : "";
            uint[] quests = unlock == 0 ? [] : [unlock];
            foreach (var todoRef in mission.MissionToDo)
            {
                if (!todoRef.IsValid) continue;
                var todo = todoRef.Value;
                for (int i = 0; i < todo.RequiredItem.Count; i++)
                    if (todo.RequiredItem[i].IsValid && todo.RequiredItem[i].Value.Item.RowId != 0)
                        Research(todo.RequiredItem[i].Value.Item.RowId, new("MissionHandIn", name, "Cosmic Exploration", Quests: quests, Zone: zone,
                            RequiredCount: todo.RequiredItemQuantity[i] > 0 ? todo.RequiredItemQuantity[i] : null, Repeatable: true));
            }
            if (mission.WKSMissionSupplyItem.IsValid)
            {
                var supply = mission.WKSMissionSupplyItem.Value;
                for (int i = 0; i < supply.Item.Count; i++)
                    if (supply.Item[i].IsValid && supply.Item[i].Value.Item.RowId != 0)
                        Research(supply.Item[i].Value.Item.RowId, new("MissionSupply", name, "Cosmic Exploration", Quests: quests, Zone: zone,
                            RequiredCount: supply.ItemCount[i] > 0 ? supply.ItemCount[i] : null, Repeatable: true));
            }
            if (!mission.WKSMissionRecipe.IsValid) continue;
            foreach (var recipeRef in mission.WKSMissionRecipe.Value.Recipe)
            {
                if (!recipeRef.IsValid) continue;
                var recipe = recipeRef.Value;
                if (recipe.ItemResult.RowId == 0) continue;
                Research(recipe.ItemResult.RowId, new("MissionCraft", name, "Cosmic Exploration", Quests: quests, Zone: zone,
                    Results: [recipe.ItemResult.RowId], Requirements: [$"{recipe.CraftType.Value.Name} · Lv {recipe.RecipeLevelTable.Value.ClassJobLevel}"], Repeatable: true));
                for (int i = 0; i < recipe.Ingredient.Count; i++)
                    if (recipe.Ingredient[i].RowId != 0 && recipe.AmountIngredient[i] > 0)
                        Research(recipe.Ingredient[i].RowId, new("MissionIngredient", name, "Cosmic Exploration", Quests: quests, Zone: zone,
                            RequiredCount: recipe.AmountIngredient[i], Results: [recipe.ItemResult.RowId], Repeatable: true));
            }
        }
    }
}
