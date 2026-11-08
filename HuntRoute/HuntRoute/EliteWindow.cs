using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
namespace HuntRoute;

public sealed class EliteWindow : Window {
    private readonly Plugin plugin;
    internal MobHuntEntry? Selected;
    private int page;
    internal bool WorldMarkers = true;
    internal int MaxDistance = 300;
    public EliteWindow(Plugin plugin) : base("Недельная охота — места появления###HuntRouteElite") {
        this.plugin=plugin; Size=new(720,800); SizeCondition=ImGuiCond.FirstUseEver;

    }
    public override void PreDraw() => HuntTheme.Push();
    public override void PostDraw() => HuntTheme.Pop();
    internal void Show(MobHuntEntry mob) { Selected=mob; page=0; IsOpen=true; }
    public override void Draw() {
        if(!plugin.Ready || Selected==null) { ImGui.Text("Войдите в игру и выберите недельную цель."); return; }
        var mob=Selected;
        if(!plugin.Mobs.Any(m=>m.BillNumber==mob.BillNumber && m.MobIndex==mob.MobIndex && m.MobHuntId==mob.MobHuntId)) {
            Selected=null; ImGui.TextWrapped("Этот контракт больше не активен. Выберите текущую недельную цель."); return;
        }
        ImGui.TextColored(new Vector4(.4f,.85f,.78f,1),mob.Name+" / "+mob.TerritoryName);
        ImGui.TextWrapped("Жёлтые точки — известные места появления выбранной цели. Это возможные точки, а не её текущая позиция.");
        ImGui.Checkbox("Метки обнаруженной цели в игровом мире",ref WorldMarkers);
        ImGui.SliderInt("Дальность, м",ref MaxDistance,50,500);
        var points=EliteSpawns.For(mob);
        if(points.Length==0) { ImGui.Text("Для этой цели нет проверенных точек появления."); return; }
        ImGui.TextDisabled($"Известных мест: {points.Length}. Нажатие точки откроет игровую карту.");
        if(ImGui.Button("Отметить группу на игровой карте")) ShowNative(points);
        ImGui.SameLine();
        if(ImGui.Button("Следующие 12")) { page=(page+1)%((points.Length+11)/12); ShowNative(points); }
        ImGui.TextDisabled($"Группа {page+1}/{(points.Length+11)/12} (игровая карта вмещает 12 временных меток)");
        var map=Service.DataManager.GetExcelSheet<Map>().GetRow(mob.MapId);
        string id=map.Id.ToString();
        var texture=Service.TextureProvider.GetFromGame($"ui/map/{id}/{id.Replace("/", "")}_m.tex").GetWrapOrDefault();
        float size=Math.Max(200,Math.Min(ImGui.GetContentRegionAvail().X,ImGui.GetContentRegionAvail().Y-30));
        var origin=ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("spawn-map",new Vector2(size,size));
        var draw=ImGui.GetWindowDrawList();
        if(texture!=null) draw.AddImage(texture.Handle,origin,origin+new Vector2(size));
        else draw.AddRectFilled(origin,origin+new Vector2(size),0xff30302a);
        Vector2 Pixel(Vector2 p) => origin+MapCoordinates.ToTexturePixel(p,map)/2048f*size;
        for(int i=0;i<points.Length;i++) {
            var center=Pixel(points[i]);
            if(center.X<origin.X || center.Y<origin.Y || center.X>origin.X+size || center.Y>origin.Y+size) continue;
            draw.AddCircleFilled(center,5,0xff45cfff); draw.AddCircle(center,6,0xff202020);
            if(Vector2.Distance(ImGui.GetMousePos(),center)<9 && ImGui.IsItemHovered()) {
                ImGui.SetTooltip($"Место {i+1}: X {points[i].X:F1}, Y {points[i].Y:F1}");
                if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) Service.GameGui.OpenMapWithMapLink(MapCoordinates.CreateLink(mob.TerritoryType,map,points[i]));
            }
        }
        if(Service.ClientState.TerritoryType==mob.TerritoryType && Service.ClientState.MapId==mob.MapId && Service.ObjectTable.LocalPlayer is {} player) {
            var p=MapUtil.WorldToMap(new Vector2(player.Position.X,player.Position.Z),map);
            draw.AddCircleFilled(Pixel(p),6,0xffffff40);
        }
        ImGui.TextDisabled("Источник точек: ffxiv-huntmaps-maker (MIT). Голубая точка — вы.");
    }
    private unsafe void ShowNative(Vector2[] points) {
        if(Selected==null) return;
        var map=AgentMap.Instance(); if(map==null) return;
        var row=Service.DataManager.GetExcelSheet<Map>().GetRow(Selected.MapId);
        map->TempMapMarkerCount=0;
        foreach(var p in points.Skip(page*12).Take(12)) {
            var world=Location.ToWorld(p,row);
            map->AddGatheringTempMarker((int)world.X,(int)world.Y,25,60004,4,"Possible B-rank spawn");
        }
        map->OpenMap(Selected.MapId,Selected.TerritoryType,Selected.Name+" — possible spawns",FFXIVClientStructs.FFXIV.Client.UI.Agent.MapType.GatheringLog);
    }
    internal void DrawWorld() {
        if(!plugin.Settings.WorldMarkers || !plugin.Ready || Service.ObjectTable.LocalPlayer is not {} player || Service.Condition[ConditionFlag.WatchingCutscene] || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.BetweenAreas] || Service.Condition[ConditionFlag.BetweenAreas51]) return;
        foreach(var npc in plugin.DetectedElites()) {
            float distance=Vector3.Distance(player.Position,npc.Position);
            if(!float.IsFinite(distance)||distance>plugin.Settings.WorldDistance || !Service.GameGui.WorldToScreen(npc.Position+new Vector3(0,3,0),out var screen)) continue;
            var label=$"[B] {npc.Name} · {distance:F0} м";
            var draw=ImGui.GetBackgroundDrawList();
            var textSize=ImGui.CalcTextSize(label); var top=screen-new Vector2(textSize.X/2,30);
            draw.AddRectFilled(top-new Vector2(8,5),top+textSize+new Vector2(8,5),0xdd161a16,5);
            draw.AddText(top,0xff88ff77,label); draw.AddCircle(screen,9,0xff88ff77,16,3);
        }
    }
}
