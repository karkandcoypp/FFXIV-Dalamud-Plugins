using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
namespace HuntRoute;

public sealed partial class Plugin {
 private readonly EliteMarkerSound eliteMarkerSound=new();
 internal bool ObserveRenderedMarker(WorldHuntMarker marker) =>
  eliteMarkerSound.Observe(marker,Settings.EliteSound,Service.ClientState.TerritoryType,Service.ClientState.MapId);

 internal WorldHuntMarker[] ReadWorldHuntMarkers(){
  if(!Ready||!Settings.WorldMarkers&&!Settings.OrdinaryWorldMarkers||!Service.ClientState.IsLoggedIn||Service.ObjectTable.LocalPlayer is not {} player||Service.Condition.Any(ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.Unconscious))return [];
  uint territory=Service.ClientState.TerritoryType,map=Service.ClientState.MapId;
  var goals=Mobs.Where(m=>AllTiers.Contains(m.BillNumber)&&m.TerritoryType==territory&&m.MapId==map)
   .Select(m=>new WorldHuntGoal(JourneyFlow.Key(m),m.MobHuntId,m.Name??"Цель охоты",m.TerritoryType,m.MapId,m.IsEliteMark,Kills(m),m.NeededKills,!unreadBills.Contains(m.BillNumber),Flow.Excluded(m))).ToArray();
  var names=goals.Where(g=>g.Known&&!g.Excluded&&g.Kills<g.Needed&&(g.Elite?Settings.WorldMarkers:Settings.OrdinaryWorldMarkers)).Select(g=>g.NameId).ToHashSet();
  if(names.Count==0)return [];
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>names.Contains(n.NameId)).Select(n=>new WorldHuntActor(n.GameObjectId,n.NameId,n.Position,n.IsDead)).ToArray();
  string? key=Flow.Active&&Session.Target is {} target?JourneyFlow.Key(target):null;
  ulong? actor=Session.Target is {IsEliteMark:true}?Search.ActorId:ordinaryKind==key?ordinaryActor:null;
  return WorldHuntMarkers.Build(goals,actors,player.Position,territory,map,Settings,key,actor);
 }
}
