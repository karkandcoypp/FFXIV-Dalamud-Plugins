using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
namespace HuntRoute;

public static class MapCoordinates {
    public static Vector2 ToWorld(Vector2 position,Map map) {
        var zero=MapUtil.WorldToMap(Vector2.Zero,map);
        var delta=MapUtil.WorldToMap(new Vector2(100,100),map)-zero;
        return (position-zero)/delta*100;
    }
    public static MapLinkPayload CreateLink(uint territory,Map map,Vector2 position) {
        var world=ToWorld(position,map);
        // Integer MapLinkPayload coordinates are world units * 1000, not displayed map coordinates * 1000.
        return new MapLinkPayload(territory,map.RowId,(int)MathF.Round(world.X*1000),(int)MathF.Round(world.Y*1000));
    }
}
