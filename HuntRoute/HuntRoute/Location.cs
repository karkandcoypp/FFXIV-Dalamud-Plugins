using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace HuntRoute;

public static class Location {
    public record PositionInfo(float X, float Y) { public Vector2 Coordinate => new(X,Y); }
    public static readonly Dictionary<uint,PositionInfo> Database = Load();
    private static Dictionary<uint,PositionInfo> Load() {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("HuntRoute.daily-locations.json")!;
        return JsonSerializer.Deserialize<Dictionary<uint,float[]>>(stream)!.ToDictionary(p=>p.Key,p=>new PositionInfo(p.Value[0],p.Value[1]));
    }
    public static void CreateMapMarker(uint territory, uint map, uint mob, string? name) {
        if(!Database.TryGetValue(mob,out var p)) return;
        Service.GameGui.OpenMapWithMapLink(MapCoordinates.CreateLink(territory,Service.DataManager.GetExcelSheet<Map>().GetRow(map),p.Coordinate));
    }
    public static unsafe bool SetMapFlag(uint territory,uint mapId,uint mob) {
        if(!Database.TryGetValue(mob,out var position)) return false;
        var map=Service.DataManager.GetExcelSheet<Map>().GetRow(mapId);
        if(map.TerritoryType.RowId!=territory) return false;
        var world=MapCoordinates.ToWorld(position.Coordinate,map);
        var agent=AgentMap.Instance(); if(agent==null) return false;
        agent->FlagMarkerCount=0;
        agent->SetFlagMapMarker(territory,mapId,world.X,world.Y);
        return true;
    }
    public static Vector2 ToWorld(Vector2 position,Map map) => MapCoordinates.ToWorld(position,map);
}
