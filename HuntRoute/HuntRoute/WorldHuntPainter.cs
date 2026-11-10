using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

public static class WorldHuntPainter {
 public static void Draw(WorldHuntMarker marker,Vector2 screen){
  if(!float.IsFinite(screen.X)||!float.IsFinite(screen.Y))return;
  string label=marker.Label;var size=ImGui.CalcTextSize(label);var top=screen-new Vector2(size.X/2,30);
  var draw=ImGui.GetBackgroundDrawList();
  draw.AddRectFilled(top-new Vector2(8,5),top+size+new Vector2(8,5),marker.Current?0xf5223028:0xdd161a16,5);
  draw.AddText(top,marker.Color,label);draw.AddCircle(screen,marker.Radius,0xff151515,16,marker.Thickness+3);draw.AddCircle(screen,marker.Radius,marker.Color,16,marker.Thickness);
 }
}
