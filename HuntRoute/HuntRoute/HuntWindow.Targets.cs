using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

public sealed partial class HuntWindow {
 private void DrawTargets(){
  if(ImGui.SmallButton("К охоте"))page=HuntPage.Hunt;
  ImGui.SameLine();ImGui.TextUnformatted("Все цели");
  Colored(HuntTheme.Muted,"Нажмите на имя для действий");
  if(plugin.Flow.Active&&plugin.Session.Target is {} active){
   ImGui.TextWrapped("Сейчас: "+active.Name);if(ImGui.SmallButton("К текущей цели"))plugin.FocusTarget();
  }
  if(ImGui.CollapsingHeader("Фильтры")){
   ImGui.SetNextItemWidth(-1);
   if(ImGui.BeginCombo("##expansion",expansion<0?"Все дополнения":Plugin.Boards[expansion].Name)){
    if(ImGui.Selectable("Все дополнения",expansion<0))expansion=-1;
    for(int i=0;i<Plugin.Boards.Length;i++)if(ImGui.Selectable(Plugin.Boards[i].Name,expansion==i))expansion=i;
    ImGui.EndCombo();
   }
   ImGui.SetNextItemWidth(-1);
   if(ImGui.BeginCombo("##zone",zone==""?"Все зоны":zone)){
    if(ImGui.Selectable("Все зоны",zone==""))zone="";
    foreach(var name in plugin.Routes.Select(r=>r.Name).Distinct())if(ImGui.Selectable(name,zone==name))zone=name;
    ImGui.EndCombo();
   }
   ImGui.Checkbox("Скрыть выполненные",ref plugin.HideDone);ImGui.Checkbox("Только отложенные",ref plugin.DeferredOpen);
  }
  var all=plugin.Mobs.Where(m=>Plugin.AllTiers.Contains(m.BillNumber)).ToArray();
  var remaining=all.Where(m=>!plugin.Done(m)).ToArray();
  var shown=plugin.Routes.SelectMany(r=>r.Mobs).Where(m=>Plugin.AllTiers.Contains(m.BillNumber))
   .Where(m=>(expansion<0||Plugin.Boards[expansion].Daily.Contains(m.BillNumber)||Plugin.Boards[expansion].Weekly==m.BillNumber)&&(zone==""||m.TerritoryName==zone)&&(!plugin.HideDone||!plugin.Done(m))&&(!plugin.DeferredOpen||plugin.Flow.Excluded(m)||plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m)))).DistinctBy(JourneyFlow.Key).ToArray();
  if(shown.Length==0){
   var empty=EmptyView.Describe(true,all.Length,remaining.Length,remaining.Count(plugin.Flow.Excluded),remaining.Count(m=>!plugin.Flow.Excluded(m)&&plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m))),0);
   ImGui.TextWrapped(empty.Text);
   if(ImGui.Button(empty.Action)){if(empty.Kind is EmptyKind.NoBills or EmptyKind.Completed)page=HuntPage.Boards;else{expansion=-1;zone="";plugin.HideDone=false;plugin.DeferredOpen=empty.Kind is EmptyKind.Deferred or EmptyKind.Unavailable;}}
  }
  foreach(var group in shown.GroupBy(m=>(m.TerritoryType,m.MapId))){
   var first=group.First();int left=group.Count(m=>!plugin.Done(m));
   ImGui.PushID($"{group.Key.TerritoryType}:{group.Key.MapId}");
   bool focus=plugin.Selected!=null&&group.Any(m=>JourneyFlow.Key(m)==plugin.Selected);
   if(focus&&revealSelected)ImGui.SetNextItemOpen(true,ImGuiCond.Always);
   var flags=first.TerritoryType==plugin.CurrentTerritory||shown.Select(m=>m.TerritoryType).Distinct().Count()==1?ImGuiTreeNodeFlags.DefaultOpen:ImGuiTreeNodeFlags.None;
   if(ImGui.CollapsingHeader($"{first.TerritoryName} · {left}",flags))foreach(var mob in group)DrawTarget(mob);
   ImGui.PopID();
  }
  revealSelected=false;
 }
 private void DrawTarget(MobHuntEntry mob){
  string key=JourneyFlow.Key(mob);bool selected=plugin.Selected==key;
  bool current=plugin.Session.Target is {} active&&JourneyFlow.Key(active)==key;
  ImGui.PushID(key);if(!HuntTheme.BeginCard("target")){ImGui.PopID();return;}
  if(current)Colored(HuntTheme.Accent,"Текущая цель");
  ImGui.TextWrapped((mob.IsEliteMark?"Элита · ":"")+mob.Name);
  if(ImGui.IsItemClicked())plugin.Selected=selected?null:key;MobPreview.OnHover(mob,plugin.Kills(mob));
  string state=plugin.Done(mob)?"Выполнено":plugin.Flow.Excluded(mob)?"Отложено":plugin.Flow.Unavailable.ContainsKey(key)?"Недоступно":"";
  ImGui.TextUnformatted($"{plugin.Kills(mob)} / {mob.NeededKills}"+(state.Length>0?" · "+state:""));
  if(selected){
   bool excluded=plugin.Flow.Excluded(mob);ImGui.BeginDisabled(plugin.Done(mob)||excluded);
   if(ImGui.Button("Сопровождать"))plugin.Track(mob);ImGui.EndDisabled();ImGui.SameLine();
   if(ImGui.Button("Карта"))plugin.ShowMap(mob);
   if(ImGui.SmallButton(excluded?"Вернуть в маршрут":"Отложить цель")){if(excluded)plugin.RestoreDeferred(mob);else plugin.Defer(mob);}
   if(!excluded&&ImGui.SmallButton("Отложить зону"))plugin.Defer(mob,true);
   if(plugin.Flow.Unavailable.TryGetValue(key,out var reason))ImGui.TextWrapped(reason);
  }
  HuntTheme.EndCard();ImGui.PopID();
 }
}
