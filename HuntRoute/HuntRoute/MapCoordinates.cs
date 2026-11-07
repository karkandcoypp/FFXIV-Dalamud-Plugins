using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
namespace HuntRoute;

public static class MapCoordinates {
    private static void Validate(Vector2 position,Map map) {
        if(map.SizeFactor==0 || !float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position),"Некорректные координаты карты.");
    }
    public static Vector2 FromTexturePixel(Vector2 pixel,Map map) {
        Validate(pixel,map);
        return MapUtil.WorldToMap((pixel-new Vector2(1024))/(map.SizeFactor/100f)-new Vector2(map.OffsetX,map.OffsetY),map);
    }
    public static Vector2 ToTexturePixel(Vector2 position,Map map) =>
        (ToWorld(position,map)+new Vector2(map.OffsetX,map.OffsetY))*(map.SizeFactor/100f)+new Vector2(1024);
    public static Vector2 ToWorld(Vector2 position,Map map) {
        Validate(position,map);
        var zero=MapUtil.WorldToMap(Vector2.Zero,map);
        var delta=MapUtil.WorldToMap(new Vector2(100,100),map)-zero;
        return (position-zero)/delta*100;
    }
    public static MapLinkPayload CreateLink(uint territory,Map map,Vector2 position) {
        if(territory==0 || map.TerritoryType.RowId!=territory) throw new ArgumentException("Карта не принадлежит выбранной зоне.");
        var world=ToWorld(position,map);
        // Integer MapLinkPayload coordinates are world units * 1000, not displayed map coordinates * 1000.
        return new MapLinkPayload(territory,map.RowId,checked((int)MathF.Round(world.X*1000)),checked((int)MathF.Round(world.Y*1000)));
    }
}
