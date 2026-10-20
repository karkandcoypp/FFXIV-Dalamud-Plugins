using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;

internal static class RouteDiagnostics
{
    public static void Run(string path)
    {
        using var game = new GameData(path);
        var items = game.Excel.GetSheet<Item>(Language.English);
        foreach (var territory in game.Excel.GetSheet<TerritoryType>(Language.English).Where(t => t.PlaceName.Value.Name.ToString().Contains("South Horn")))
        {
            string bg = territory.Bg.ToString();
            Console.WriteLine($"TERRITORY {territory.RowId} map{territory.Map.RowId} BG {bg}");
            if (!bg.Contains("/level/")) continue;
            string filePath = "bg/" + bg[..bg.IndexOf("/level/", StringComparison.Ordinal)] + "/level/planevent.lgb";
            var file = game.GetFile<LgbFile>(filePath);
            if (file == null) continue;
            foreach (var layer in file.Layers)
                foreach (var instance in layer.InstanceObjects)
                    if (instance.Object is LayerCommon.ENPCInstanceObject npc && npc.ParentData.ParentData.BaseId == 1053612)
                        Console.WriteLine($"LGB NPC {npc.ParentData.ParentData.BaseId}: {filePath} {instance.Transform.Translation.X}, {instance.Transform.Translation.Z}");
        }
        var ids = items.Where(i => new[] { "Aetherspun Gold", "Aetherpool Grip", "Aloalo Potsherd", "Copper Ore", "Allagan Tin Piece" }.Contains(i.Name.ToString())).Select(i => i.RowId).ToHashSet();
        foreach (var id in ids) Console.WriteLine($"ITEM {id}: {items.GetRow(id).Name} / {items.GetRow(id).Description}");
        var questSheet = game.Excel.GetSheet<Quest>(Language.English);
        var samples = new Dictionary<string, int>();
        foreach (var q in questSheet)
        {
            foreach (var p in q.QuestParams.Where(p => p.ScriptInstruction.ToString().Contains("ITEM")))
            {
                string instruction = p.ScriptInstruction.ToString();
                string family = new(instruction.Where(c => !char.IsDigit(c)).ToArray());
                samples.TryAdd(family, 0);
                if (samples[family]++ < 2) Console.WriteLine($"PARAM {q.RowId} {q.Name}: {instruction}={p.ScriptArg}");
                if (ids.Contains(p.ScriptArg)) Console.WriteLine($"MATCH QUEST {q.RowId} {q.Name}: {instruction}={p.ScriptArg} NPC {q.TargetEnd.RowId}");
            }
        }
        var special = game.Excel.GetSheet<SpecialShop>(Language.English);
        var matches = new HashSet<uint>();
        foreach (var shop in special)
            foreach (var entry in shop.Item)
                if (entry.ItemCosts.Any(c => ids.Contains(c.ItemCost.RowId) && c.CurrencyCost > 0))
                {
                    matches.Add(shop.RowId);
                    Console.WriteLine($"SHOP {shop.RowId} {shop.Name} type{shop.UseCurrencyType} quest{shop.Quest.RowId} custom{shop.CustomTalk.RowId}: " +
                        string.Join(" + ", entry.ItemCosts.Where(c => c.CurrencyCost > 0).Select(c => $"{c.ItemCost.RowId}:{c.CurrencyCost} type{c.CostType}")) + " -> " +
                        string.Join(", ", entry.ReceiveItems.Where(r => r.Item.RowId != 0).Select(r => r.Item.Value.Name.ToString())));
                }
        var residents = game.Excel.GetSheet<ENpcResident>(Language.English);
        var talkMatches = new HashSet<uint>();
        foreach (var talk in game.Excel.GetSheet<CustomTalk>(Language.English))
            foreach (var script in talk.Script.Where(s => matches.Contains(s.ScriptArg)))
            {
                Console.WriteLine($"SHOP HANDLER {talk.RowId} {talk.MainOption}: {script.ScriptInstruction}={script.ScriptArg}");
                talkMatches.Add(talk.RowId);
            }
        var seriesMatches = game.Excel.GetSubrowSheet<InclusionShopSeries>().SelectMany(g => g)
            .Where(s => matches.Contains(s.SpecialShop.RowId)).Select(s => s.RowId).ToHashSet();
        var categoryMatches = game.Excel.GetSheet<InclusionShopCategory>().Where(c => seriesMatches.Contains(c.InclusionShopSeries.RowId)).Select(c => c.RowId).ToHashSet();
        foreach (var inclusion in game.Excel.GetSheet<InclusionShop>().Where(s => s.Category.Any(c => categoryMatches.Contains(c.RowId))))
        { Console.WriteLine($"SHOP INCLUSION {inclusion.RowId} {inclusion.ShopName}"); talkMatches.Add(inclusion.RowId); }
        foreach (var npc in game.Excel.GetSheet<ENpcBase>(Language.English))
        {
            var name = residents.GetRow(npc.RowId).Singular.ToString();
            if (npc.ENpcData.Any(l => talkMatches.Contains(l.RowId)) || name.Contains("expedition armorer", StringComparison.OrdinalIgnoreCase) || new[] { "Valeroine", "E-Una-Kotor", "Koh Rabntah", "Trisassant", "Bango Zango", "Roarich", "Maisenta" }.Contains(name))
            {
                Console.WriteLine($"NPC {npc.RowId} {name}");
                foreach (var link in npc.ENpcData.Where(l => l.RowId != 0))
                {
                    Console.WriteLine(" LINK " + link.RowId);
                    if (link.TryGetValue<CustomTalk>(out var talk))
                        foreach (var script in talk.Script.Where(s => s.ScriptArg != 0)) Console.WriteLine($"  {script.ScriptInstruction}={script.ScriptArg}");
                }
                if (name.Contains("expedition armorer", StringComparison.OrdinalIgnoreCase))
                    foreach (var level in game.Excel.GetSheet<Level>().Where(l => l.Object.RowId == npc.RowId))
                        Console.WriteLine($" POSITION type{level.Type} map{level.Map.RowId} territory{level.Territory.RowId} x{level.X} z{level.Z}");
            }
        }
    }
}
