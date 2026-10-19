using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace EorzeaCompass;

public sealed record VendorDefinition(string Id, string Name, string Group, uint Expansion, string[] StoryIds, uint[] Npcs,
    uint[] SpecialShops, uint[] GilShops, string[] NpcNames, Dictionary<string, string> ShopNames, uint Tribe = 0, Dictionary<string, uint[]>? ShopNpcs = null);
public sealed record RewardCost(uint ItemId, string Name, uint Count, ushort Collectability = 0);
public sealed record RewardRequirement(string Kind, uint Id, string Name, int Value = 0)
{ public string Key => $"{Kind}:{Id}:{Value}"; }
public sealed record VendorPosition(string Npc, string Zone, uint Territory, uint Map, float X, float Y);
public sealed record VendorOffer(string SourceId, string Source, string Group, uint Expansion, uint ShopId, string Shop,
    RewardCost[] Costs, RewardRequirement[] Requirements, VendorPosition[] Locations, string[] Npcs);

public static class CollectionRules
{
    public static string Kind(uint action, string name) => action switch
    {
        1322 => "Mount",
        853 => "Minion",
        25183 => "Music",
        3357 => "Card",
        29459 => "Portrait",
        20086 => "Accessory",
        1013 => "Barding",
        37312 => "Glasses",
        2633 when name.StartsWith("Modern Aesthetics", StringComparison.Ordinal) => "Hairstyle",
        2633 when name.StartsWith("Ballroom Etiquette", StringComparison.Ordinal) => "Emote",
        2633 => "Unlock",
        43141 => "Record",
        19743 => "FieldNote",
        _ => ""
    };
    // Field notes have no dependable global unlock bit in API 15. Never call them missing.
    public static bool CanTrack(string kind) => kind.Length > 0 && kind != "FieldNote";
    public static string State(bool characterReady, bool supported, bool unlocked) => !characterReady || !supported ? "unknown" : unlocked ? "collected" : "missing";
    public static string Conditions(IEnumerable<bool?> requirements)
    {
        var values = requirements.ToArray(); return values.Any(v => v == false) ? "locked" : values.Any(v => v == null) ? "unknown" : "met";
    }
}

public static class VendorCatalog
{
    public static IReadOnlyList<QuestReward> Build(ExcelModule excel, IReadOnlyList<Story> stories, ICollection<string>? warnings = null)
    {
        using var stream = typeof(VendorCatalog).Assembly.GetManifestResourceStream("EorzeaCompass.vendors.json") ?? throw new InvalidDataException("Vendor catalog missing");
        var definitions = JsonSerializer.Deserialize<VendorDefinition[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var specials = excel.GetSheet<SpecialShop>(Language.English); var gils = excel.GetSheet<GilShop>(Language.English);
        var gilItems = excel.GetSubrowSheet<GilShopItem>(Language.English);
        var npcs = excel.GetSheet<ENpcBase>(Language.English); var names = excel.GetSheet<ENpcResident>(Language.English);
        var quests = excel.GetSheet<Quest>(Language.English); var achievements = excel.GetSheet<Achievement>(Language.English);
        var levels = excel.GetSheet<Level>(); var maps = excel.GetSheet<Map>(Language.English);
        var locationCache = new Dictionary<uint, VendorPosition[]>();
        VendorPosition[] Locate(uint id)
        {
            if (locationCache.TryGetValue(id, out var cached)) return cached;
            var result = new List<VendorPosition>();
            foreach (var l in levels.Where(l => l.Type == 8 && l.Object.RowId == id))
            {
                if (!maps.TryGetRow(l.Map.RowId, out var m) || m.SizeFactor == 0 || m.TerritoryType.RowId != l.Territory.RowId) continue;
                float C(float value, short offset) => (float)(41 / (m.SizeFactor / 100d) * (((value + offset) * m.SizeFactor / 100d + 1024) / 2048) + 1);
                result.Add(new(names.GetRow(id).Singular.ToString(), m.PlaceName.Value.Name.ToString(), l.Territory.RowId, m.RowId, C(l.X, m.OffsetX), C(l.Z, m.OffsetY)));
            }
            return locationCache[id] = result.Distinct().ToArray();
        }
        var rewards = new List<QuestReward>();
        foreach (var d in definitions)
        {
            if (d.StoryIds.Any(id => !stories.Any(s => s.Definition.Id == id)) || d.Npcs.Length != d.NpcNames.Length
                || d.Npcs.Where((id, i) => !names.TryGetRow(id, out var name) || name.Singular.ToString() != d.NpcNames[i]).Any())
            { warnings?.Add(d.Name); continue; }
            var paths = new Dictionary<uint, List<(RewardRequirement[] Rules, uint Npc)>>();
            uint walkingNpc = 0;
            void Walk(RowRef link, List<RewardRequirement> requirements, HashSet<uint> seen)
            {
                if (!seen.Add(link.RowId)) return;
                if (link.Is<SpecialShop>() || link.Is<GilShop>())
                {
                    if (!paths.TryGetValue(link.RowId, out var found)) paths[link.RowId] = found = new();
                    found.Add((requirements.ToArray(), walkingNpc)); return;
                }
                if (link.TryGetValue<PreHandler>(out var p))
                {
                    var next = new List<RewardRequirement>(requirements); AddQuest(next, p.UnlockQuest.RowId);
                    Walk(p.Target, next, new(seen));
                }
                else if (link.TryGetValue<TopicSelect>(out var t)) foreach (var child in t.Shop.Where(s => s.RowId != 0)) Walk(child, new(requirements), new(seen));
                else if (link.TryGetValue<CustomTalk>(out var t2))
                {
                    var next = new List<RewardRequirement>(requirements);
                    foreach (var s in t2.Script.Where(s => s.ScriptInstruction.ToString().Contains("QUEST", StringComparison.Ordinal) && quests.HasRow(s.ScriptArg))) AddQuest(next, s.ScriptArg);
                    foreach (var s in t2.Script.Where(s => s.ScriptInstruction.ToString().Contains("SHOP", StringComparison.Ordinal)))
                    {
                        if (specials.HasRow(s.ScriptArg) || gils.HasRow(s.ScriptArg))
                        {
                            if (!paths.TryGetValue(s.ScriptArg, out var found)) paths[s.ScriptArg] = found = new();
                            found.Add((next.ToArray(), walkingNpc));
                        }
                    }
                }
            }
            void AddQuest(List<RewardRequirement> list, uint id)
            {
                if (id == 0) return;
                list.Add(new("quest", id, quests.TryGetRow(id, out var q) ? q.Name.ToString() : $"Quest #{id}"));
            }
            foreach (var id in d.Npcs) { walkingNpc = id; foreach (var link in npcs.GetRow(id).ENpcData.Where(l => l.RowId != 0)) Walk(link, new(), new()); }
            foreach (var id in d.SpecialShops.Concat(d.GilShops)) if (!paths.ContainsKey(id))
            {
                if (!specials.HasRow(id) && !gils.HasRow(id)) { warnings?.Add(d.Name); continue; }
                paths[id] = new();
                foreach (var npc in d.ShopNpcs?.GetValueOrDefault(id.ToString()) ?? d.Npcs)
                    paths[id].Add(([new("unknown", id, "NPC access / additional shop conditions")], npc));
            }
            foreach (var path in paths)
            {
                // Alternative NPC routes are separate offers, never combined into an AND gate.
                foreach (var route in path.Value.GroupBy(r => string.Join('|', r.Rules.Select(x => x.Key))))
                {
                    var requirements = new List<RewardRequirement>(route.First().Rules);
                    var offerNpcs = route.Any(r => r.Npc == 0) ? d.Npcs : route.Select(r => r.Npc).Distinct().ToArray();
                    var shopName = "";
                    void Add(Item item, uint count, List<RewardRequirement> rules, RewardCost[] costs)
                    {
                        var kind = CollectionRules.Kind(item.ItemAction.RowId == 0 ? 0 : item.ItemAction.Value.Action.RowId, item.Name.ToString());
                        if (kind.Length == 0 || count == 0 || costs.Length == 0) return;
                        var offer = new VendorOffer(d.Id, d.Name, d.Group, d.Expansion, path.Key, shopName, costs, rules.DistinctBy(r => r.Key).ToArray(), offerNpcs.SelectMany(Locate).Distinct().ToArray(), offerNpcs.Select(id => names.GetRow(id).Singular.ToString()).Distinct().ToArray());
                        rewards.Add(new(0, item.RowId, item.Name.ToString(), (int)count, RewardKind.Vendor, item.ItemUICategory.Value.Name.ToString(), 0, 0, item.Icon, d.StoryIds, kind, offer));
                    }
                    if (specials.TryGetRow(path.Key, out var shop))
                    {
                        shopName = shop.Name.ToString();
                        if (d.ShopNames.TryGetValue(path.Key.ToString(), out var expected) && expected != shopName) { warnings?.Add(d.Name); continue; }
                        if (shop.RequiredFestival.RowId != 0) continue;
                        AddQuest(requirements, shop.Quest.RowId);
                        if (shop.RequiredContentFinderCondition.RowId != 0) requirements.Add(new(shop.RequiredContentFinderConditionComplete ? "duty-clear" : "duty-unlock", shop.RequiredContentFinderCondition.RowId, shop.RequiredContentFinderCondition.Value.Name.ToString()));
                        if (shop.CustomTalk.RowId != 0) requirements.Add(new("unknown", shop.CustomTalk.RowId, "Additional NPC conditions"));
                        foreach (var row in shop.Item)
                        {
                            var rules = new List<RewardRequirement>(requirements); AddQuest(rules, row.Quest.RowId);
                            if (row.AchievementUnlock.RowId != 0) rules.Add(new("achievement", row.AchievementUnlock.RowId, achievements.GetRow(row.AchievementUnlock.RowId).Name.ToString()));
                            if (row.Unknown0.Any(v => v != 0) || row.Unknown1.Any(v => v != 0) || row.Unknown2 != 0) rules.Add(new("unknown", path.Key, "Additional shop conditions"));
                            var costs = row.ItemCosts.Where(c => c.CurrencyCost > 0).Select(c =>
                            {
                                // Types 2 and 4 with CostType 0 are direct item/currency IDs. Other schemes use token indices.
                                bool direct = c.CostType == 0 && shop.UseCurrencyType is 2 or 4;
                                return new RewardCost(direct ? c.ItemCost.RowId : 0, direct ? c.ItemCost.Value.Name.ToString() : "Unresolved currency", c.CurrencyCost, c.CollectabilityCost);
                            }).ToArray();
                            if (costs.Any(c => c.ItemId == 0)) rules.Add(new("unknown", path.Key, "Currency conversion needs verification"));
                            foreach (var receive in row.ReceiveItems.Where(r => r.Item.RowId != 0)) Add(receive.Item.Value, receive.ReceiveCount, rules, costs);
                        }
                    }
                    else if (gils.TryGetRow(path.Key, out var gil))
                    {
                        shopName = gil.Name.ToString(); if (gil.FestivalId != 0) continue; AddQuest(requirements, gil.Quest.RowId);
                        if (!gilItems.HasRow(path.Key)) continue;
                        foreach (var row in gilItems.GetRow(path.Key))
                        {
                            if (row.Item.RowId == 0) continue; var item = row.Item.Value;
                            var rules = new List<RewardRequirement>(requirements);
                            foreach (var q in row.QuestRequired.Where(q => q.RowId != 0))
                            {
                                if (row.StateRequired == 100) AddQuest(rules, q.RowId);
                                else rules.Add(new("unknown", q.RowId, q.Value.Name.ToString()));
                            }
                            if (row.AchievementRequired.RowId != 0) rules.Add(new("achievement", row.AchievementRequired.RowId, achievements.GetRow(row.AchievementRequired.RowId).Name.ToString()));
                            if (row.Unknown1 != 0 || row.Unknown_70_1 != 0 || row.Unknown_70_2 != 0) rules.Add(new("unknown", path.Key, "Additional shop conditions"));
                            Add(item, 1, rules, [new(1, "Gil", item.PriceMid)]);
                        }
                    }
                }
            }
        }
        return rewards.DistinctBy(r => (r.ItemId, r.Vendor!.SourceId, r.Vendor.ShopId, string.Join('|', r.Vendor.Requirements.Select(q => q.Key)), string.Join('|', r.Vendor.Costs.Select(c => $"{c.ItemId}:{c.Count}")))).ToArray();
    }
}
