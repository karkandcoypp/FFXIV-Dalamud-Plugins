using System.Text.RegularExpressions;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;

namespace BagSense;

public sealed class WorldCatalog
{
    public Dictionary<uint, List<ShopRoute>> Shops { get; } = new();
    public Dictionary<uint, List<QuestUse>> QuestItems { get; } = new();
    public Dictionary<uint, NpcLocation[]> Positions { get; } = new();
    public NpcLocation[] Merchants { get; }
    public int LocatedShops => Shops.Count(p => p.Value.Any(r => r.Location.CanMap));

    public WorldCatalog(ExcelModule excel, Func<string, LgbFile?>? readLayer = null)
    {
        var names = excel.GetSheet<ENpcResident>(Language.English);
        var npcs = excel.GetSheet<ENpcBase>(Language.English);
        var maps = excel.GetSheet<Map>(Language.English);
        var quests = excel.GetSheet<Quest>(Language.English);
        var gils = excel.GetSheet<GilShop>(Language.English);
        var specials = excel.GetSheet<SpecialShop>(Language.English);
        var inclusions = excel.GetSheet<InclusionShop>(Language.English);
        var categories = excel.GetSheet<InclusionShopCategory>(Language.English);
        var series = excel.GetSubrowSheet<InclusionShopSeries>(Language.English);
        var locations = new Dictionary<uint, List<NpcLocation>>();
        foreach (var level in excel.GetSheet<Level>().Where(l => l.Type == 8 && l.Object.RowId != 0))
        {
            if (!names.TryGetRow(level.Object.RowId, out var name) || name.Singular.IsEmpty
                || !maps.TryGetRow(level.Map.RowId, out var map) || map.SizeFactor == 0 || map.TerritoryType.RowId == 0) continue;
            float Coordinate(float world, short offset) => (float)(41 / (map.SizeFactor / 100d) * (((world + offset) * map.SizeFactor / 100d + 1024) / 2048) + 1);
            var location = new NpcLocation(level.Object.RowId, name.Singular.ToString(), map.PlaceName.Value.Name.ToString(), map.TerritoryType.RowId, map.RowId,
                Coordinate(level.X, map.OffsetX), Coordinate(level.Z, map.OffsetY));
            if (!location.CanMap) continue;
            if (!locations.TryGetValue(location.NpcId, out var list)) locations[location.NpcId] = list = new();
            if (!list.Contains(location)) list.Add(location);
        }
        // South Horn shop NPCs are in planevent.lgb, not the Level sheet. Read the reviewed territory's
        // actual placement data; no NPC name, coordinates or shop-to-NPC link is guessed or hard-coded.
        if (readLayer != null && excel.GetSheet<TerritoryType>().TryGetRow(1252, out var territory)
            && maps.TryGetRow(territory.Map.RowId, out var layerMap) && layerMap.SizeFactor > 0
            && layerMap.TerritoryType.RowId == territory.RowId)
        {
            string bg = territory.Bg.ToString();
            int split = bg.IndexOf("/level/", StringComparison.Ordinal);
            var file = split < 0 ? null : readLayer("bg/" + bg[..split] + "/level/planevent.lgb");
            if (file != null)
                foreach (var layer in file.Layers.Where(l => l.FestivalID == 0))
                    foreach (var instance in layer.InstanceObjects)
                    {
                        if (instance.Object is not LayerCommon.ENPCInstanceObject npc || !names.TryGetRow(npc.ParentData.ParentData.BaseId, out var name)) continue;
                        float Coordinate(float world, short offset) => (float)(41 / (layerMap.SizeFactor / 100d)
                            * (((world + offset) * layerMap.SizeFactor / 100d + 1024) / 2048) + 1);
                        var location = new NpcLocation(name.RowId, name.Singular.ToString(), layerMap.PlaceName.Value.Name.ToString(), territory.RowId, layerMap.RowId,
                            Coordinate(instance.Transform.Translation.X, layerMap.OffsetX), Coordinate(instance.Transform.Translation.Z, layerMap.OffsetY));
                        if (!location.CanMap) continue;
                        if (!locations.TryGetValue(location.NpcId, out var list)) locations[location.NpcId] = list = new();
                        if (!list.Contains(location)) list.Add(location);
                    }
        }
        foreach (var pair in locations) Positions[pair.Key] = pair.Value.ToArray();

        RouteRequirement QuestRule(uint id) => quests.TryGetRow(id, out var q) ? new("quest", id, q.Name.ToString()) : new("unknown", id, "Unresolved quest reference");
        foreach (var npc in npcs)
        {
            if (!Positions.TryGetValue(npc.RowId, out var npcLocations)) continue;
            void Add(uint shopId, string menu, RouteRequirement[] requirements)
            {
                if (!Shops.TryGetValue(shopId, out var list)) Shops[shopId] = list = new();
                foreach (var location in npcLocations)
                {
                    var route = new ShopRoute(shopId, menu, location, requirements.Distinct().ToArray());
                    if (!list.Any(r => r.Location == location && r.Menu == menu && r.Requirements.SequenceEqual(route.Requirements))) list.Add(route);
                }
            }
            void Walk(RowRef link, string menu, RouteRequirement[] requirements, HashSet<uint> seen)
            {
                if (seen.Count > 12 || !seen.Add(link.RowId)) return;
                if (link.Is<SpecialShop>() || link.Is<GilShop>()) { Add(link.RowId, menu, requirements); return; }
                if (link.TryGetValue<PreHandler>(out var pre))
                {
                    Walk(pre.Target, menu, pre.UnlockQuest.RowId == 0 ? requirements : [..requirements, QuestRule(pre.UnlockQuest.RowId)], new(seen));
                }
                else if (link.TryGetValue<TopicSelect>(out var topic))
                {
                    foreach (var child in topic.Shop.Where(s => s.RowId != 0)) Walk(child, topic.Name.ToString(), requirements, new(seen));
                }
                else if (link.TryGetValue<InclusionShop>(out var inclusion)) ReadInclusion(inclusion, requirements);
                else if (link.TryGetValue<FateShop>(out var fate))
                {
                    foreach (var shop in fate.SpecialShop.Where(s => s.RowId != 0))
                        Add(shop.RowId, "", [..requirements, new("fate", fate.RowId, "")]);
                }
                else if (link.TryGetValue<CustomTalk>(out var talk))
                {
                    // Numeric arguments are not inferred as shops: only explicitly named SHOP instructions are followed.
                    RouteRequirement[] rules = [..requirements, new("npc", talk.RowId, talk.MainOption.ToString())];
                    foreach (var script in talk.Script.Where(s => s.ScriptArg != 0 && s.ScriptInstruction.ToString().Contains("SHOP", StringComparison.Ordinal)))
                    {
                        if (specials.HasRow(script.ScriptArg) || gils.HasRow(script.ScriptArg)) Add(script.ScriptArg, talk.MainOption.ToString(), rules);
                        else if (inclusions.TryGetRow(script.ScriptArg, out var inclusionShop)) ReadInclusion(inclusionShop, rules);
                    }
                    if (talk.SpecialLinks.RowId != 0) Walk(talk.SpecialLinks, talk.MainOption.ToString(), rules, new(seen));
                }
            }
            void ReadInclusion(InclusionShop shop, RouteRequirement[] requirements)
            {
                var rules = shop.UnlockQuest.RowId == 0 ? requirements : [..requirements, QuestRule(shop.UnlockQuest.RowId)];
                foreach (var reference in shop.Category.Where(c => c.RowId != 0))
                {
                    if (!categories.TryGetRow(reference.RowId, out var category) || !series.HasRow(category.InclusionShopSeries.RowId)) continue;
                    RouteRequirement[] categoryRules = category.ClassJobCategory.RowId == 0 ? rules
                        : [..rules, new("job", category.ClassJobCategory.RowId, category.ClassJobCategory.Value.Name.ToString())];
                    foreach (var entry in series.GetRow(category.InclusionShopSeries.RowId))
                        if (entry.SpecialShop.RowId != 0) Add(entry.SpecialShop.RowId, shop.ShopName.ToString() + " / " + category.Name.ToString(), categoryRules);
                }
            }
            foreach (var link in npc.ENpcData.Where(l => l.RowId != 0)) Walk(link, "", [], new());
        }

        // A verified regular gil-shop link supplies a concrete NPC to whom ordinary sellable items can be sold.
        Merchants = Shops.Where(p => gils.TryGetRow(p.Key, out var shop) && shop.Quest.RowId == 0 && shop.FestivalId == 0)
            .SelectMany(p => p.Value).Where(r => r.Requirements.Length == 0).Select(r => r.Location)
            .DistinctBy(l => l.Key).OrderByDescending(l => l.Npc is "Bango Zango" or "Roarich" or "Maisenta").ThenBy(l => l.Npc).ToArray();

        foreach (var quest in quests.Where(q => q.RowId != 0 && !q.Name.IsEmpty && q.JournalGenre.RowId != 0))
        {
            void AddQuestItem(uint id, bool reward, string instruction = "")
            {
                if (id == 0) return;
                bool issuer = reward || quest.TargetEnd.RowId == 0 || !Positions.ContainsKey(quest.TargetEnd.RowId);
                uint npc = issuer ? quest.IssuerStart.RowId : quest.TargetEnd.RowId;
                var use = new QuestUse(quest.RowId, quest.Name.ToString(), reward, quest.IsRepeatable, Positions.GetValueOrDefault(npc) ?? [], issuer);
                if (!reward && instruction.StartsWith("COST_ITEM_ATMA_", StringComparison.Ordinal))
                {
                    uint count = quest.QuestParams.FirstOrDefault(p => p.ScriptInstruction.ToString() == "COST_USE_ATMA").ScriptArg;
                    if (count is > 0 and < 100) use = use with { RequiredCount = (int)count, Quality = "any", Evidence = "COST_ITEM_ATMA_n + COST_USE_ATMA", Source = "installed Quest sheet" };
                }
                if (!QuestItems.TryGetValue(id, out var list)) QuestItems[id] = list = new();
                if (!list.Any(q => q.QuestId == quest.RowId && q.Reward == reward)) list.Add(use);
            }
            // RITEM0, RITEM1... identify ordinary requested items. ITEM0 points to event items in a separate ID space.
            foreach (var parameter in quest.QuestParams)
                if (IsRequestedItemInstruction(parameter.ScriptInstruction.ToString())) AddQuestItem(parameter.ScriptArg, false, parameter.ScriptInstruction.ToString());
            foreach (var reward in quest.Reward.Where(r => r.RowId != 0)) AddQuestItem(reward.RowId, true);
            foreach (var reward in quest.OptionalItemReward.Where(r => r.RowId != 0)) AddQuestItem(reward.RowId, true);
        }
    }
    public static bool IsRequestedItemInstruction(string instruction) => Regex.IsMatch(instruction, "^(RITEM[0-9]+|RITEM_ADD_[0-9]+|QUEST_ITEM_?[0-9]*|COST_ITEM(_.*)?|QST_ITEM[0-9]+|QST_PRODUCT_ITEM|QST_GATHERING_ITEM_(MIN|HRV|FSH))$", RegexOptions.CultureInvariant);
}
