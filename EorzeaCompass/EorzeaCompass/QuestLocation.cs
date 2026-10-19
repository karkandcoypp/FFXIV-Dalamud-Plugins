using System;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;

namespace EorzeaCompass;

public sealed record QuestLocation(string Npc, string Zone, uint Territory, uint Map, float X, float Y)
{
    public static QuestLocation? Read(uint id)
    {
        var quests = Plugin.Data.GetExcelSheet<Quest>(ClientLanguage.English);
        if (!quests.TryGetRow(id, out var q)
            || !Plugin.Data.GetExcelSheet<Level>().TryGetRow(q.IssuerLocation.RowId, out var location)
            || location.Type != 8 || location.Object.RowId != q.IssuerStart.RowId
            || !Plugin.Data.GetExcelSheet<Map>().TryGetRow(location.Map.RowId, out var map)
            || map.SizeFactor == 0 || map.TerritoryType.RowId != location.Territory.RowId) return null;
        var npcs = Plugin.Data.GetExcelSheet<ENpcResident>(ClientLanguage.English);
        float Coordinate(float value, short offset) => (float)(41 / (map.SizeFactor / 100d)
            * (((value + offset) * map.SizeFactor / 100d + 1024) / 2048) + 1);
        return new(npcs.TryGetRow(q.IssuerStart.RowId, out var npc) ? npc.Singular.ToString() : "",
            map.PlaceName.Value.Name.ToString(), location.Territory.RowId, map.RowId,
            Coordinate(location.X, map.OffsetX), Coordinate(location.Z, map.OffsetY));
    }

    public void Open() => Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(Territory, Map, X, Y));
}
