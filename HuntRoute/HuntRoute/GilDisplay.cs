using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace HuntRoute;

internal static class GilDisplay {
    private const uint IconId=65002;
    private const float IconSize=16,Gap=6;

    internal static IconImageNode CreateIcon(float size=IconSize)=>new(){IconId=IconId,Size=new(size,size),FitTexture=true,IsVisible=false};

    internal static void Draw(string text,string fallback){
        var texture=MobPreview.Image(IconId);
        ImGui.TextUnformatted(texture==null?fallback:text);
        if(texture==null)return;
        ImGui.SameLine(0,Gap);
        ImGui.Image(texture.Handle,new Vector2(ImGui.GetTextLineHeight()));
    }

    internal static bool Place(IconImageNode icon,TextNode text,string label,Vector2 position,Vector2 size){
        if(!icon.IsTextureReady)return false;
        var measured=text.GetTextDrawSize(label,considerScale:false);
        float labelWidth=MathF.Ceiling(measured.X);
        float iconSize=icon.Size.X;
        if(labelWidth<=0||labelWidth+iconSize+Gap+2>size.X)return false;
        float x=position.X+(size.X-labelWidth-iconSize-Gap)/2;
        icon.Position=new(x+labelWidth+Gap,text.Position.Y+(measured.Y-iconSize)/2);
        text.Position=new(x,text.Position.Y);
        // Keep the full measurement width available; a narrow node can wrap the next page's text.
        text.Size=new(position.X+size.X-x,text.Size.Y);
        text.AlignmentType=AlignmentType.TopLeft;
        text.String=label;
        icon.IsVisible=true;
        return true;
    }
}
