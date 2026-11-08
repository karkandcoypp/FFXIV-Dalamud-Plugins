using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
namespace HuntRoute;

internal static class MobPreview {
    private static readonly HashSet<uint> failures=[];
    private static IDalamudTextureWrap? Image(uint icon){
        foreach(bool high in new[]{true,false})try{
            var wrap=Service.TextureProvider.GetFromGameIcon(new GameIconLookup(icon,itemHq:false,hiRes:high)).GetWrapOrDefault();
            if(wrap!=null&&wrap.Width>0&&wrap.Height>0)return wrap;
        }catch(Exception e){if(failures.Add(icon))Service.PluginLog.Debug(e,"Изображение цели {Icon} временно недоступно",icon);}
        return null;
    }
    internal static void OnHover(MobHuntEntry mob,int kills) {
        if(!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(mob.Name ?? "Цель охоты");
        ImGui.TextDisabled($"{Math.Max(0,kills)}/{mob.NeededKills}  ·  {mob.TerritoryName}");
        if(mob.Icon!=0) {
            var texture=Image(mob.Icon);
            if(texture!=null && texture.Width>0 && texture.Height>0) {
                float scale=Math.Min(250f/texture.Width,210f/texture.Height)*ImGui.GetIO().FontGlobalScale;
                var size=new Vector2(texture.Width,texture.Height)*scale;
                var top=ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(top,top+size,0xff343a40,8);
                ImGui.Image(texture.Handle,size);
            } else ImGui.TextDisabled("Изображение загружается...");
        }
        else ImGui.TextDisabled("Для этой цели в игровых данных нет изображения.");
        if(!mob.IsEliteMark && Location.Database.TryGetValue(mob.MobHuntId,out var pos)) {
            ImGui.TextDisabled($"X {pos.X:F1}   Y {pos.Y:F1}");
            ImGui.TextDisabled("Нажмите имя, чтобы перейти к цели.");
        }
        ImGui.EndTooltip();
    }
}
