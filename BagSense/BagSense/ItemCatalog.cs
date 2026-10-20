using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace BagSense;

// Only game-sheet work happens off the framework thread. Character state is read by InventoryReader.
public sealed class ItemCatalog
{
    private readonly ExcelSheet<Item> items;
    private readonly Dictionary<uint, List<BuyOffer>> shops = new();
    private readonly Dictionary<uint, List<string>> recipes = new();
    private readonly Dictionary<uint, List<ExchangeUse>> exchanges = new();
    private readonly Dictionary<uint, ItemFacts> cache = new();
    public int MerchantItems => shops.Count;
    public int RecipeItems => recipes.Count;
    public int ExchangeItems => exchanges.Count;

    public ItemCatalog(ExcelModule excel)
    {
        items = excel.GetSheet<Item>(Language.English);
        foreach (var recipe in excel.GetSheet<Recipe>(Language.English))
        {
            if (recipe.ItemResult.RowId == 0) continue;
            string result = recipe.ItemResult.Value.Name.ToString();
            for (int i = 0; i < recipe.Ingredient.Count; i++)
            {
                uint id = recipe.Ingredient[i].RowId;
                if (id == 0 || recipe.AmountIngredient[i] == 0) continue;
                if (!recipes.TryGetValue(id, out var list)) recipes[id] = list = new();
                if (list.Count < 8 && !list.Contains(result)) list.Add(result);
            }
        }
        var shopNames = excel.GetSheet<GilShop>(Language.English);
        foreach (var group in excel.GetSubrowSheet<GilShopItem>(Language.English))
        {
            foreach (var entry in group)
            {
                if (entry.Item.RowId == 0 || !shopNames.TryGetRow(entry.RowId, out var shop)) continue;
                var item = entry.Item.Value;
                bool conditional = shop.Quest.RowId != 0 || shop.FestivalId != 0 || entry.IsHQ || entry.StateRequired != 0 || entry.QuestRequired.Any(q => q.RowId != 0)
                    || entry.AchievementRequired.RowId != 0 || entry.Unknown1 != 0 || entry.Unknown_70_1 != 0 || entry.Unknown_70_2 != 0;
                if (!shops.TryGetValue(item.RowId, out var list)) shops[item.RowId] = list = new();
                var offer = new BuyOffer(shop.Name.ToString(), item.PriceMid, conditional);
                if (!list.Contains(offer)) list.Add(offer);
            }
        }
        foreach (var shop in excel.GetSheet<SpecialShop>(Language.English))
        {
            // Other currency encodings contain token indices, not item IDs.
            if (shop.UseCurrencyType is not (2 or 4)) continue;
            foreach (var entry in shop.Item)
                foreach (var cost in entry.ItemCosts)
                {
                    if (cost.CostType != 0 || cost.ItemCost.RowId == 0 || cost.CurrencyCost == 0) continue;
                    uint id = cost.ItemCost.RowId;
                    if (!exchanges.TryGetValue(id, out var list)) exchanges[id] = list = new();
                    foreach (var reward in entry.ReceiveItems.Where(r => r.Item.RowId != 0 && r.ReceiveCount > 0))
                    {
                        var use = new ExchangeUse(shop.Name.ToString(), reward.Item.Value.Name.ToString(), cost.CurrencyCost);
                        if (list.Count < 8 && !list.Contains(use)) list.Add(use);
                    }
                }
        }
    }

    public ItemFacts? Find(uint id)
    {
        if (cache.TryGetValue(id, out var cached)) return cached;
        if (!items.TryGetRow(id, out var row) || row.Name.IsEmpty) return null;
        return cache[id] = new(id, row.Name.ToString(), row.Icon, row.ItemUICategory.Value.Name.ToString(), row.Description.ToString(),
            row.EquipSlotCategory.RowId != 0, row.IsIndisposable, !row.IsUntradable, row.PriceLow, row.Desynth > 0,
            Offers: shops.GetValueOrDefault(id)?.Distinct().OrderBy(o => o.Conditional).ThenBy(o => o.Price).Take(8).ToArray(),
            Recipes: recipes.GetValueOrDefault(id)?.ToArray(), Exchanges: exchanges.GetValueOrDefault(id)?.ToArray());
    }

    public IEnumerable<ItemFacts> Search(string query)
    {
        if (query.Trim().Length < 2) yield break;
        bool byId = uint.TryParse(query, out var id);
        int found = 0;
        foreach (var row in items)
        {
            if (row.RowId == 0 || row.Name.IsEmpty || (!byId && !row.Name.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)) || (byId && row.RowId != id)) continue;
            var item = Find(row.RowId);
            if (item != null) { yield return item; if (++found == 40) yield break; }
        }
    }
}
