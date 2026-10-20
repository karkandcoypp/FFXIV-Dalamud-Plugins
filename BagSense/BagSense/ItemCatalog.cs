using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Data.Files;

namespace BagSense;

// Only game-sheet work happens off the framework thread. Character state is read by InventoryReader.
public sealed partial class ItemCatalog
{
    private readonly ExcelSheet<Item> items;
    private readonly ExcelSheet<Mount> mounts;
    private readonly ExcelSheet<MountTransient> mountText;
    private readonly Dictionary<uint, List<BuyOffer>> shops = new();
    private readonly Dictionary<uint, List<string>> recipes = new();
    private readonly Dictionary<uint, List<ExchangeUse>> exchanges = new();
    private readonly Dictionary<uint, List<DeliveryUse>> deliveries = new();
    private readonly Dictionary<uint, ItemFacts> cache = new();
    private Dictionary<uint, CatalogItem> remoteItems = new();
    private Dictionary<uint, CatalogNeed[]> remoteNeeds = new();
    private Dictionary<uint, ItemSource[]> remoteSources = new();
    public CatalogDocument? Database { get; private set; }
    public uint[] CollectionItemIds { get; }
    public int ItemCount => Math.Max(items.Count, remoteItems.Count);
    public void ApplyDatabase(CatalogDocument database)
    {
        remoteItems = database.Items.ToDictionary(i => i.Id);
        remoteNeeds = database.Needs.GroupBy(n => n.ItemId).ToDictionary(g => g.Key, g => g.ToArray());
        remoteSources = (database.Sources ?? []).GroupBy(s => s.ItemId).ToDictionary(g => g.Key,
            g => g.Select(s => new ItemSource(s.Kind, s.Name, s.Id, s.Detail)).Distinct().ToArray());
        Database = database;
        cache.Clear();
    }
    public WorldCatalog World { get; }
    public int MerchantItems => shops.Count;
    public int RecipeItems => recipes.Count;
    public int ExchangeItems => exchanges.Count;

    public ItemCatalog(ExcelModule excel, Func<string, LgbFile?>? readLayer = null)
    {
        items = excel.GetSheet<Item>(Language.English);
        mounts = excel.GetSheet<Mount>(Language.English);
        mountText = excel.GetSheet<MountTransient>(Language.English);
        CollectionItemIds = items.Where(i => i.ItemAction.IsValid && i.ItemAction.Value.Action.RowId is
            1322 or 853 or 25183 or 3357 or 29459 or 20086 or 1013 or 37312 or 2633 or 43141 or 19743).Select(i => i.RowId).ToArray();
        World = new WorldCatalog(excel, readLayer);
        foreach (var recipe in excel.GetSheet<Recipe>(Language.English))
        {
            if (recipe.ItemResult.RowId == 0) continue;
            string result = recipe.ItemResult.Value.Name.ToString();
            for (int i = 0; i < recipe.Ingredient.Count; i++)
            {
                uint id = recipe.Ingredient[i].RowId;
                if (id == 0 || recipe.AmountIngredient[i] == 0) continue;
                if (!recipes.TryGetValue(id, out var list)) recipes[id] = list = new();
                if (!list.Contains(result)) list.Add(result);
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
                var offer = new BuyOffer(shop.Name.ToString(), item.PriceMid, conditional, shop.RowId,
                    World.Shops.GetValueOrDefault(shop.RowId)?.ToArray());
                if (!list.Any(o => o.ShopId == offer.ShopId && o.Price == offer.Price && o.Conditional == offer.Conditional)) list.Add(offer);
            }
        }
        IndexKnowledge(excel);
        IndexDeliveries(excel);
        var quests = excel.GetSheet<Quest>(Language.English);
        var achievements = excel.GetSheet<Achievement>(Language.English);
        var duties = excel.GetSheet<ContentFinderCondition>(Language.English);
        var seenExchanges = new HashSet<string>();
        foreach (var shop in excel.GetSheet<SpecialShop>(Language.English))
        {
            // Other currency encodings contain token indices, not item IDs.
            if (shop.UseCurrencyType is not (2 or 4))
            {
                foreach (var reward in shop.Item.SelectMany(e => e.ReceiveItems).Where(r => r.Item.RowId != 0 && r.ReceiveCount > 0))
                    Source(reward.Item.RowId, new("SpecialShop", shop.Name.ToString(), shop.RowId, "Unresolved currency encoding"));
                continue;
            }
            foreach (var entry in shop.Item)
            {
                var costs = entry.ItemCosts.Where(c => c.CurrencyCost > 0).Select(c =>
                    new TradeCost(c.CostType == 0 ? c.ItemCost.RowId : 0, c.CostType == 0 && c.ItemCost.RowId != 0
                        ? c.ItemCost.Value.Name.ToString() : "Unresolved currency", c.CurrencyCost, c.CollectabilityCost)).ToArray();
                var rules = new List<RouteRequirement>();
                void QuestRule(uint id)
                {
                    if (id == 0) return;
                    rules.Add(quests.TryGetRow(id, out var quest) ? new("quest", id, quest.Name.ToString()) : new("unknown", id, "Unresolved quest reference"));
                }
                QuestRule(shop.Quest.RowId); QuestRule(entry.Quest.RowId);
                if (shop.RequiredContentFinderCondition.RowId != 0) rules.Add(duties.TryGetRow(shop.RequiredContentFinderCondition.RowId, out var duty)
                    ? new("duty", duty.RowId, duty.Name.ToString()) : new("unknown", shop.RequiredContentFinderCondition.RowId, "Unresolved duty reference"));
                if (shop.RequiredFestival.RowId != 0) rules.Add(new("event", shop.RequiredFestival.RowId, ""));
                if (shop.CustomTalk.RowId != 0) rules.Add(new("npc", shop.CustomTalk.RowId, ""));
                if (entry.AchievementUnlock.RowId != 0) rules.Add(achievements.TryGetRow(entry.AchievementUnlock.RowId, out var achievement)
                    ? new("achievement", achievement.RowId, achievement.Name.ToString()) : new("unknown", entry.AchievementUnlock.RowId, "Unresolved achievement reference"));
                if (entry.Unknown0.Any(x => x != 0) || entry.Unknown1.Any(x => x != 0) || entry.Unknown2 != 0) rules.Add(new("unknown", shop.RowId, ""));
                foreach (var cost in costs.Where(c => c.ItemId != 0))
                {
                    uint id = cost.ItemId;
                    if (!exchanges.TryGetValue(id, out var list)) exchanges[id] = list = new();
                    foreach (var reward in entry.ReceiveItems.Where(r => r.Item.RowId != 0 && r.ReceiveCount > 0))
                    {
                        var use = new ExchangeUse(shop.Name.ToString(), reward.Item.Value.Name.ToString(), cost.Count, shop.RowId, reward.Item.RowId,
                            reward.ReceiveCount, costs, World.Shops.GetValueOrDefault(shop.RowId)?.ToArray(), rules.Distinct().ToArray());
                        if (seenExchanges.Add($"{id}:{use.Key}")) list.Add(use);
                    }
                }
            }
        }
        foreach (var offer in exchanges.Values.SelectMany(e => e).DistinctBy(e => e.Key))
        {
            if (!acquisitionTrades.TryGetValue(offer.RewardId, out var list)) acquisitionTrades[offer.RewardId] = list = new();
            list.Add(offer);
        }
    }

    public ItemFacts? Find(uint id)
    {
        if (cache.TryGetValue(id, out var cached)) return cached;
        if (!items.TryGetRow(id, out var row) || row.Name.IsEmpty)
            return remoteItems.TryGetValue(id, out var remote) ? new(id, remote.Name, remote.Icon, remote.Category, remote.Description,
                Indisposable: remote.EventItem, Quests: QuestUses(id), Role: remote.EventItem ? "KeyItem" : "Unresolved") : null;
        return cache[id] = new(id, row.Name.ToString(), row.Icon, row.ItemUICategory.Value.Name.ToString(), row.Description.ToString(),
            row.EquipSlotCategory.RowId != 0, row.IsIndisposable, !row.IsUntradable, row.PriceLow, row.Desynth > 0,
            Offers: shops.GetValueOrDefault(id)?.Distinct().OrderBy(o => o.Conditional).ThenBy(o => o.Price).ToArray(),
            Recipes: recipes.GetValueOrDefault(id)?.ToArray(), Exchanges: exchanges.GetValueOrDefault(id)?.ToArray(),
            Quests: QuestUses(id), Merchants: World.Merchants, Reward: Reward(row), Deliveries: deliveries.GetValueOrDefault(id)?.ToArray(), Role: Role(row), Sources: (sources.GetValueOrDefault(id) ?? []).Concat(remoteSources.GetValueOrDefault(id) ?? []).Distinct().ToArray(),
            RecipeLinks: recipeLinks.GetValueOrDefault(id)?.ToArray(), Acquisitions: acquisitionTrades.GetValueOrDefault(id)?.ToArray());
    }

    private RewardDetails Reward(Item item)
    {
        uint action = item.ItemAction.RowId == 0 ? 0 : item.ItemAction.Value.Action.RowId;
        string kind = RewardPolicy.Kind(action, item.EquipSlotCategory.RowId != 0, item.ItemUICategory.Value.Name.ToString(), item.Name.ToString());
        if (kind == "Mount" && item.ItemAction.Value.Data.Count > 0 && mounts.TryGetRow(item.ItemAction.Value.Data[0], out var mount))
        {
            string description = mountText.TryGetRow(mount.RowId, out var text) ? text.Description.ToString() : item.Description.ToString();
            return new(kind, mount.Singular.ToString(), description, mount.Icon == 0 ? 0 : (uint)(mount.Icon + 64000), mount.RowId);
        }
        return new(kind, item.Name.ToString(), item.Description.ToString(), Detail: kind == "Equipment"
            ? $"{item.ItemUICategory.Value.Name} · iLvl {item.LevelItem.RowId} · Lv {item.LevelEquip} · {item.ClassJobCategory.Value.Name}" : "");
    }

    private QuestUse[] QuestUses(uint id)
    {
        var local = World.QuestItems.GetValueOrDefault(id)?.ToList() ?? [];
        foreach (var need in remoteNeeds.GetValueOrDefault(id) ?? [])
        {
            // Never use a quest/item association for a different installed quest name.
            var old = local.FirstOrDefault(q => q.QuestId == need.QuestId && !q.Reward);
            if (old != null && old.Name != need.QuestName) continue;
            if (old != null) local.Remove(old);
            local.Add(new(need.QuestId, need.QuestName, false, need.Repeatable,
                need.Contacts.Length > 0 ? need.Contacts : need.Objective.Length > 0 ? [] : old?.Contacts ?? [],
                need.Contacts.Length == 0 && (old?.IssuerContact ?? true), need.Count, need.Quality,
                need.Objective, need.TodoIndex, need.CompleteSequence, need.Evidence, need.Source,
                need.Variants?.Select(v => $"{v.Job}: {v.Count}" + (v.Hq ? " HQ" : "")).ToArray(), need.Variants));
        }
        return local.ToArray();
    }

    public CatalogItem[] Browse(string query, int skip, int take)
    {
        var all = Database?.Items ?? items.Where(i => !i.Name.IsEmpty).Select(i => new CatalogItem(i.RowId, i.Name.ToString(), i.Icon,
            i.ItemUICategory.Value.Name.ToString(), i.Description.ToString())).ToArray();
        bool numeric = uint.TryParse(query, out uint id);
        return all.Where(i => query.Length == 0 || (numeric ? i.Id == id : i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(i => i.Id).Skip(skip).Take(take).ToArray();
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
