using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

internal static class InstalledDataChecks
{
    public static void Run(string sqpack, string feed)
    {
        using var data = new GameData(sqpack);
        using var json = JsonDocument.Parse(File.ReadAllText(feed));
        var e = json.RootElement.GetProperty("events")[0];
        var maps = data.GetExcelSheet<Map>(Language.English);
        var territories = data.GetExcelSheet<TerritoryType>(Language.English);
        var residents = data.GetExcelSheet<ENpcResident>(Language.English);
        var npcIds = e.GetProperty("locations").EnumerateArray()
            .Select(loc => uint.Parse(loc.GetProperty("id").GetString()!.Split('-')[1])).ToHashSet();
        var positions = data.GetExcelSheet<Level>(Language.English)
            .Where(level => level.Type == 8 && npcIds.Contains(level.Object.RowId)).ToList();
        foreach (var loc in e.GetProperty("locations").EnumerateArray())
        {
            var mapId = loc.GetProperty("mapId").GetUInt32();
            var territory = loc.GetProperty("territoryId").GetUInt32();
            Check(maps.TryGetRow(mapId, out var map) && map.TerritoryType.RowId == territory && territories.TryGetRow(territory, out _),
                "installed game accepts map/territory: " + loc.GetProperty("npc").GetString());
            var npc = uint.Parse(loc.GetProperty("id").GetString()!.Split('-')[1]);
            Check(residents.TryGetRow(npc, out _), "NPC exists in the installed game: " + npc);
            double ToMap(double world, double offset) => 41 / (map.SizeFactor / 100d) * (((world + offset) * map.SizeFactor / 100d + 1024) / 2048) + 1;
            Check(positions.Any(level => level.Object.RowId == npc && level.Map.RowId == mapId
                && Math.Abs(ToMap(level.X, map.OffsetX) - loc.GetProperty("x").GetDouble()) < .002
                && Math.Abs(ToMap(level.Z, map.OffsetY) - loc.GetProperty("y").GetDouble()) < .002),
                "NPC coordinates exist in installed Level/Map data: " + npc);
        }
        var items = data.GetExcelSheet<Item>(Language.English);
        var mountItem = items.GetRow(41491);
        var mountAction = data.GetExcelSheet<ItemAction>().GetRow(mountItem.ItemAction.RowId);
        Check(mountItem.Name.ToString() == "Rhiyes Identification Key" && mountAction.Action.RowId == 1322,
            "installed game identifies mount rewards by action 1322 rather than generic Other category");
        Check(data.GetExcelSheet<ItemUICategory>(Language.English).GetRow(81).Name.ToString() == "Minion"
            && data.GetExcelSheet<ItemUICategory>(Language.English).GetRow(57).Name.ToString() == "Furnishing",
            "installed category IDs match minion and housing filters");
        var requested = new Dictionary<uint, (long Price, uint Currency)>();
        foreach (var reward in e.GetProperty("tracking").GetProperty("rewards").EnumerateArray())
        {
            var id = reward.GetProperty("id").GetUInt32();
            Check(items.TryGetRow(id, out var item) && item.Icon == reward.GetProperty("icon").GetUInt32(), "installed reward ID/icon: " + id);
            var price = reward.GetProperty("price").GetInt64();
            if (price > 0) requested[id] = (price, reward.GetProperty("currencyItemId").GetUInt32());
        }
        var found = new HashSet<uint>();
        foreach (var shop in data.GetExcelSheet<SpecialShop>(Language.English))
        {
            if (shop.UseCurrencyType != 4) continue;
            foreach (var offer in shop.Item)
            {
                var id = offer.ReceiveItems[0].Item.RowId;
                if (requested.TryGetValue(id, out var wanted) && offer.ReceiveItems[0].ReceiveCount == 1
                    && offer.ItemCosts[0].ItemCost.RowId == wanted.Currency && offer.ItemCosts[0].CurrencyCost == wanted.Price)
                    found.Add(id);
            }
        }
        Check(found.SetEquals(requested.Keys), "all " + requested.Count + " published prices match installed SpecialShop data");
        var quests = data.GetExcelSheet<Quest>(Language.English);
        foreach (var q in e.GetProperty("tracking").GetProperty("quests").EnumerateArray())
        {
            Check(quests.TryGetRow(q.GetProperty("id").GetUInt32(), out var row), "quest exists in installed game: " + q.GetProperty("name").GetString());
            if (!q.TryGetProperty("locationId", out var locationId) || locationId.GetString() == "") continue;
            var loc = e.GetProperty("locations").EnumerateArray().Single(l => l.GetProperty("id").GetString() == locationId.GetString());
            var level = row.IssuerLocation.Value;
            var map = maps.GetRow(loc.GetProperty("mapId").GetUInt32());
            Check(row.IssuerStart.RowId == uint.Parse(loc.GetProperty("id").GetString()!.Split('-')[1])
                && level.Map.RowId == map.RowId, "quest issuer/map match installed game");
            double ToMap(double world, double offset) => 41 / (map.SizeFactor / 100d) * (((world + offset) * map.SizeFactor / 100d + 1024) / 2048) + 1;
            Check(Math.Abs(ToMap(level.X, map.OffsetX) - loc.GetProperty("x").GetDouble()) < .002
                && Math.Abs(ToMap(level.Z, map.OffsetY) - loc.GetProperty("y").GetDouble()) < .002,
                "quest coordinates agree with installed Level/Map data");
        }
        var saucer = quests.GetRow(65970);
        Check(saucer.Name.ToString() == "It Could Happen to You" && saucer.ClassJobLevel[0] == 15,
            "Gold Saucer unlock level comes from installed quest data");
        Check(saucer.PreviousQuestJoin == 2 && saucer.PreviousQuest.Count(q => q.RowId != 0) == 3,
            "installed Gold Saucer unlock requires ANY of the three envoy quests");
        var crowning = quests.GetRow(70337);
        Check(crowning.PreviousQuest.Any(q => q.RowId == saucer.RowId),
            "Blunderville prerequisite points to Gold Saucer unlock");
        Console.WriteLine("READ-ONLY CHECK COMPLETE: installed data verified; no game process was controlled and no map was opened.");
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
