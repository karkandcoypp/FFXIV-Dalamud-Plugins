using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
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
    private readonly Dictionary<uint, Vector2[]> spawns;
    internal bool WorldMarkers = true;
    internal int MaxDistance = 300;
    public EliteWindow(Plugin plugin) : base("Недельная охота — места появления###HuntRouteElite") {
        this.plugin=plugin; Size=new(720,800); SizeCondition=ImGuiCond.FirstUseEver;
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("HuntRoute.elite-spawns.json")!;
        var source=JsonSerializer.Deserialize<Dictionary<uint,float[][]>>(stream)!;
        spawns=source.ToDictionary(p=>p.Key,p=>p.Value.Select(v=>new Vector2(v[0],v[1])).ToArray());
    }
    internal void Show(MobHuntEntry mob) { Selected=mob; page=0; IsOpen=true; }
    public override void Draw() {
        if(!plugin.Ready || Selected==null) { ImGui.Text("Войдите в игру и выберите недельную цель."); return; }
        var mob=Selected;
        ImGui.TextColored(new Vector4(.4f,.85f,.78f,1),mob.Name+" / "+mob.TerritoryName);
        ImGui.TextWrapped("Жёлтые точки — предполагаемые места B-рангов в зоне. Они не подтверждают присутствие цели; точный набор мест может отличаться для конкретного моба.");
        ImGui.Checkbox("Метки обнаруженной цели в игровом мире",ref WorldMarkers);
        ImGui.SliderInt("Дальность, м",ref MaxDistance,50,500);
        if(!spawns.TryGetValue(mob.TerritoryType,out var points) || points.Length==0) { ImGui.Text("Для этой зоны нет точек появления."); return; }
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
        Vector2 Pixel(Vector2 p) => origin+(p-Vector2.One)*(map.SizeFactor/100f*50f/2048f)*size;
        for(int i=0;i<points.Length;i++) {
            var center=Pixel(points[i]);
            draw.AddCircleFilled(center,5,0xff45cfff); draw.AddCircle(center,6,0xff202020);
            if(Vector2.Distance(ImGui.GetMousePos(),center)<9 && ImGui.IsItemHovered()) {
                ImGui.SetTooltip($"Место {i+1}: X {points[i].X:F1}, Y {points[i].Y:F1}");
                if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) Service.GameGui.OpenMapWithMapLink(new MapLinkPayload(mob.TerritoryType,mob.MapId,(int)(points[i].X*1000),(int)(points[i].Y*1000)));
            }
        }
        if(Service.ClientState.TerritoryType==mob.TerritoryType && Service.ObjectTable.LocalPlayer is {} player) {
            var p=MapUtil.WorldToMap(new Vector2(player.Position.X,player.Position.Z),map);
            draw.AddCircleFilled(Pixel(p),6,0xffffff40);
        }
        ImGui.TextDisabled("Источник точек: HuntHelper (MIT). Голубая точка — вы.");
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
        if(!WorldMarkers || !plugin.Ready || Service.ObjectTable.LocalPlayer is not {} player || Service.Condition[ConditionFlag.WatchingCutscene] || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.BetweenAreas] || Service.Condition[ConditionFlag.BetweenAreas51]) return;
        var targets=plugin.Mobs.Where(m=>m.IsEliteMark && m.TerritoryType==Service.ClientState.TerritoryType && !plugin.Done(m)).Select(m=>m.MobHuntId).ToHashSet();
        if(targets.Count==0) return;
        foreach(var npc in Service.ObjectTable.OfType<IBattleNpc>()) {
            if(npc.IsDead || !targets.Contains(npc.NameId)) continue;
            float distance=Vector3.Distance(player.Position,npc.Position);
            if(distance>MaxDistance || !Service.GameGui.WorldToScreen(npc.Position+new Vector3(0,3,0),out var screen)) continue;
            var label=$"[B] {npc.Name} · {distance:F0} м";
            var draw=ImGui.GetBackgroundDrawList();
            var textSize=ImGui.CalcTextSize(label); var top=screen-new Vector2(textSize.X/2,30);
            draw.AddRectFilled(top-new Vector2(8,5),top+textSize+new Vector2(8,5),0xdd161a16,5);
            draw.AddText(top,0xff88ff77,label); draw.AddCircle(screen,9,0xff88ff77,16,3);
        }
    }
}
