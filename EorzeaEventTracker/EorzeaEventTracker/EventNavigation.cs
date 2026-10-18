using System;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;

namespace EorzeaEventTracker;

internal static class EventNavigation
{
    public static bool Open(EventLocation location)
    {
        if (!Plugin.Client.IsLoggedIn) return false;
        try
        {
            // Check online IDs against the installed game before creating a map payload.
            if (!Plugin.Data.GetExcelSheet<Map>().TryGetRow(location.MapId, out var map)
                || map.TerritoryType.RowId != location.TerritoryId
                || !Plugin.Data.GetExcelSheet<TerritoryType>().TryGetRow(location.TerritoryId, out _)) return false;
            return Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(location.TerritoryId, location.MapId, location.X, location.Y));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not open event NPC map");
            return false;
        }
    }
}
