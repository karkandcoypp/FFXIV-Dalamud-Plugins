using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Map=Lumina.Excel.Sheets.Map;
namespace HuntRoute;
public readonly record struct MarkerStamp(uint Territory,uint Map,float X,float Y) {
 public bool Matches(MarkerStamp? other)=>other is {} p && p.Territory==Territory && p.Map==Map && Vector2.Distance(new(X,Y),new(p.X,p.Y))<2;
}
public sealed partial class Plugin {
 internal Func<bool>? FlightCapability {get;set;}
 internal Func<string>? ContextProvider {get;set;}
 internal Func<long>? Clock {get;set;}
 internal Func<MarkerStamp?>? MarkerReader {get;set;}
 internal Func<MarkerStamp,bool>? MarkerWriter {get;set;}
 internal Func<uint,bool>? TeleportTransport {get;set;}
 internal long Now=>Clock?.Invoke()??Environment.TickCount64;
 private bool stateLoadPending;
 private HuntStore? store;
 internal HuntStore Store=>store??=new();
 private JourneyFlow? flow;
 internal JourneyFlow Flow=>flow??=new();
 internal HuntSettings Settings=>Store.Settings;
 private string? stateFile,lastSaved,context;
 private long nextStateSave,contractRevision,contractAt;
 private PendingJourney? pendingJourney;
 private MarkerStamp? ownedMarker;
 private ulong? ordinaryActor;
 private Vector2? ordinaryPosition;
 private EliteProgress? restoreSearch;
 private Dictionary<string,Vector2>? live;
 private Dictionary<string,Vector2> livePositions=>live??=new();
 private string? ordinaryKind;
 private bool externalTransition;
 internal string? Selected;
 internal bool FocusRequested;
 internal void FocusTarget(){if(Session.Target!=null)Selected=JourneyFlow.Key(Session.Target);FocusRequested=true;window.IsOpen=true;}
 internal string? NoticeText;
 internal bool PickupOpen,SettingsOpen,DeferredOpen;
 private readonly Queue<string> journal=new();
 private string? lastJournal;
 internal SavedJourney CharacterSave {
  get {if(!Store.Characters.TryGetValue(character,out var s))Store.Characters[character]=s=new();return s;}
 }
 private void InitializeStore(){
  stateFile=Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(),"journey-state.json");
  try{store=HuntStore.Load(stateFile);}catch(Exception e){stateLoadPending=true;store=new();Service.PluginLog.Error(e,"Не удалось прочитать состояние маршрута");NoticeText="Сохранение маршрута недоступно; история контрактов хранится отдельно.";}
 }
 private void LoadJourney(){
  contractRevision=0;contractAt=0;flow=new();Flow.Load(CharacterSave);ordinaryActor=null;ordinaryKind=null;context=null;ownedMarker=null;pendingJourney=null;
  if(Flow.Active)Flow.Status="Сохранённый маршрут на паузе. Проверьте контракты и нажмите «Продолжить».";
 }
 internal void SaveJourney(bool force=false){
  if(stateFile==null || character==0 || (!force && Now<nextStateSave))return;
  var s=CharacterSave;s.Context=context;s.Active=Flow.Active;s.Manual=Flow.Manual;s.Target=Session.Target is {} t?JourneyFlow.Key(t):Flow.RestoreTarget;
  s.Deferred=Flow.Deferred.ToHashSet();s.Zones=Flow.Zones.ToHashSet();s.LongNotified=Flow.LongNotified.ToHashSet();
  if(!Flow.Restored){s.SearchTarget=searchKey;s.Search=Session.Target is {IsEliteMark:true} currentSearch&&searchKey==SearchIdentity(currentSearch)?Search.Capture():null;}
  if(stateLoadPending){try{var recovered=HuntStore.Load(stateFile);recovered.Characters[character]=s;store=recovered;stateLoadPending=false;}catch(Exception e){nextStateSave=Now+10000;NoticeText="Сохранение маршрута временно недоступно. Исходный файл не перезаписан.";Service.PluginLog.Debug(e,"Повторное чтение маршрута");return;}}
  string value=JsonSerializer.Serialize(Store,new JsonSerializerOptions{IncludeFields=true});
  if(!force && value==lastSaved)return;
  try{Store.Save(stateFile);lastSaved=value;nextStateSave=Now+2000;}catch(Exception e){nextStateSave=Now+10000;NoticeText="Не удалось сохранить маршрут: после перезапуска изменения могут пропасть.";Service.PluginLog.Error(e,"Сохранение маршрута");}
 }
 internal static byte[] AllTiers=>Boards.SelectMany(b=>b.Daily.Append(b.Weekly)).ToArray();
 internal bool Eligible(MobHuntEntry m){
  if(unreadBills.Contains(m.BillNumber)||!AllTiers.Contains(m.BillNumber)||Done(m)||Flow.Excluded(m)||!Mobs.Any(current=>JourneyFlow.Key(current)==JourneyFlow.Key(m)))return false;
  try {
   var map=Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId);
   if(map.SizeFactor==0||map.TerritoryType.RowId!=m.TerritoryType)throw new InvalidDataException("Карта не принадлежит зоне");
   if(Flow.Active&&CurrentTerritory!=m.TerritoryType&&EntryFor(m)==null){Flow.Unavailable[JourneyFlow.Key(m)]="Нет открытого эфирита или проверенного входа";return false;}
   return HasPosition(m);
  }catch(Exception e){Flow.Unavailable[JourneyFlow.Key(m)]="Координаты временно недоступны: "+e.Message;return false;}
 }
 private void ResetTargetTracking(){Search.Start([]);searchKey="";ordinaryActor=null;ordinaryKind=null;ordinaryPosition=null;}
 private string SearchIdentity(MobHuntEntry target)=>$"{Session.RunId}:{JourneyFlow.Key(target)}";
 internal bool NavigationAllowed=>!Flow.Active || Flow.Pauses==PauseReason.None;
 internal void StopJourney(){Flow.Stop();Session.Stop();Search.Start([]);searchKey="";pendingJourney=null;ordinaryActor=null;ownedMarker=null;NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");NativeDialogs.Cancel("travel");SaveJourney(true);}
 internal void TogglePause(){if(!Flow.Active)return;if(Flow.Pauses.HasFlag(PauseReason.User))ResumeJourney();else{Flow.Pauses|=PauseReason.User;Search.Suspend();Flow.Status="Пауза по вашему выбору";SaveJourney(true);}}
 internal void ResumeJourney(){
  if(!Ready)return;
  if(pendingJourney!=null){NoticeText="Текущее перемещение ещё не завершено. Маршрут будет обновлён после прибытия.";return;}
  RefreshLive();
  Flow.Pauses&=~(PauseReason.User|PauseReason.Restored|PauseReason.Marker|PauseReason.TravelError|PauseReason.Unavailable|PauseReason.FlagError|PauseReason.Board|PauseReason.NoZone);
  ownedMarker=null;Search.Suspend();
  var target=Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==(Flow.Manual??Flow.RestoreTarget)&&Eligible(m));
  if(target==null && Flow.Manual!=null){Flow.Manual=null;Flow.Status="Ручная цель больше не активна.";}
  target??=Session.Target is {} old?Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==JourneyFlow.Key(old)&&Eligible(m)):null;
  target??=BestTarget();
  if(target==null){Flow.Status="Нет доступных целей. Проверьте отложенные контракты.";NoticeText=Flow.Status;SaveJourney(true);return;}
  bool starting=!Flow.Active || Session.Phase==RoutePhase.Idle;
  if(Flow.Restored && CharacterSave.Target==JourneyFlow.Key(target))restoreSearch=CharacterSave.Search;
  Flow.Active=true;Flow.Restored=false;Flow.RestoreTarget=null;
  if(CurrentTerritory==target.TerritoryType){if(starting)Session.Start(target,AllTiers);else Session.Continue(target);Session.RetryMarker();Search.Retry();AdvanceRoute();}
  else OfferTravel(target,starting);
  SaveJourney(true);
 }
 private float ZoneCost(ZoneRoute route) {
  Vector2? last=route.Start?.Id==75&&route.Mobs.FirstOrDefault()?.TerritoryType==399?null:route.Start?.Position;float cost=0;
  foreach(var m in route.Mobs.Where(m=>!Done(m)&&!Flow.Excluded(m))) {
   if(Position(m) is not {} pos)continue;
   var map=Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId);
   if(last is {} previous)cost+=Vector2.Distance(MapCoordinates.ToWorld(previous,map),MapCoordinates.ToWorld(pos,map));last=pos;
  }
  return cost;
 }
 internal void SetMini(bool enabled){Settings.Mini=enabled;if(miniWindow!=null)miniWindow.IsOpen=enabled;SaveJourney(true);}
 internal void NextOffered(){if(Flow.Active)Ping("Цели зоны выполнены. Доступен переход в следующую зону.",true);}
 internal MobHuntEntry? BestTarget()=>Routes.SelectMany(r=>r.Mobs).Where(Eligible).OrderBy(m=>m.TerritoryType==CurrentTerritory?0:1).FirstOrDefault();
 internal void Track(MobHuntEntry m){
  if(!Ready||Done(m)||Flow.Excluded(m))return;
  if(pendingJourney!=null){NoticeText="Дождитесь завершения текущего перемещения, затем выберите цель.";return;}
  RefreshLive();if(!HasPosition(m)){NoticeText="Для этой цели пока нет доступной позиции. Она остаётся незавершённой.";return;}
  if(Session.Target is {} prior&&JourneyFlow.Key(prior)!=JourneyFlow.Key(m))ResetTargetTracking();
  Flow.Manual=JourneyFlow.Key(m);Flow.Active=true;Flow.Pauses&=~(PauseReason.User|PauseReason.Restored|PauseReason.TravelError|PauseReason.NoZone|PauseReason.Board);
  if(CurrentTerritory==m.TerritoryType){if(Session.Phase is RoutePhase.Idle or RoutePhase.Finished)Session.Start(m,AllTiers);else Session.Continue(m);AdvanceRoute();}
  else OfferTravel(m,Session.Phase==RoutePhase.Idle);
  SaveJourney(true);
 }
 internal void Defer(MobHuntEntry m,bool zone=false){if(zone)Flow.Zones.Add(m.TerritoryType);else Flow.Deferred.Add(JourneyFlow.Key(m));if(Flow.Manual==JourneyFlow.Key(m)||zone&&Session.Target?.TerritoryType==m.TerritoryType)Flow.Manual=null;NativeDialogs.Cancel("travel");routeSignature="";AdvanceRoute();SaveJourney(true);}
 internal void RestoreDeferred(MobHuntEntry m){Flow.Deferred.Remove(JourneyFlow.Key(m));Flow.Zones.Remove(m.TerritoryType);routeSignature="";SaveJourney(true);}
 private Vector2? Position(MobHuntEntry m){
  if(livePositions.TryGetValue(JourneyFlow.Key(m),out var observed))return MapUtil.WorldToMap(observed,Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId));
  if(m.IsEliteMark){
   var points=EliteSpawns.For(m);if(points.Length==0)return null;
   if(CurrentTerritory==m.TerritoryType && CurrentMap==m.MapId && Service.ObjectTable.LocalPlayer is {} player){var map=Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId);var here=MapUtil.WorldToMap(new(player.Position.X,player.Position.Z),map);return points.MinBy(p=>Vector2.DistanceSquared(p,here));}
   var entries=Entries(m.MapId,m.TerritoryType);return entries.Count==0?points[0]:points.MinBy(p=>entries.Min(e=>Vector2.DistanceSquared(p,e.Position)));
  }
  return Location.Database.TryGetValue(m.MobHuntId,out var p)?p.Coordinate:null;
 }
 internal EntryPoint? EntryFor(MobHuntEntry m){var positions=m.IsEliteMark?EliteSpawns.For(m):Position(m) is {} p?new[]{p}:Array.Empty<Vector2>();return Entries(m.MapId,m.TerritoryType).OrderBy(e=>positions.Length==0?float.MaxValue:positions.Min(p=>Vector2.DistanceSquared(e.Position,p))).ThenBy(e=>e.Id).FirstOrDefault() ?? (m.TerritoryType==399&&Service.Aetherytes.Any(a=>a.AetheryteId==75)?new(75,"Idyllshire",Vector2.Zero):null);}
 internal uint Price(uint id)=>Service.Aetherytes.Where(a=>a.AetheryteId==id).Select(a=>a.GilCost).DefaultIfEmpty(uint.MaxValue).Min();
 private void OfferTravel(MobHuntEntry target,bool starting){
  var entry=EntryFor(target);if(entry==null){Flow.Unavailable[JourneyFlow.Key(target)]="Нет открытого эфирита или проверенного входа";Flow.Status="Цель отложена: нет открытого входа.";return;}
  string key=JourneyFlow.Key(target);uint run=Session.RunId;uint quote=Price(entry.Id);
  NativeDialogs.Offer(new("travel",$"travel:{run}:{key}:{entry.Id}:{quote}","Продолжить охоту?",
   ()=>Ready && !Done(target)&&!Flow.Excluded(target)&&Flow.Active&&Session.RunId==run&&Mobs.Any(m=>JourneyFlow.Key(m)==key)&&EntryFor(target)?.Id==entry.Id&&Price(entry.Id)<=quote,
   ()=>!TravelBusy && !Flow.Pauses.HasFlag(PauseReason.Marker),
   ()=>{var current=EntryFor(target);if(current?.Id!=entry.Id || Price(entry.Id)>quote)return false;Travel(target);return Session.Target is {} t&&JourneyFlow.Key(t)==key&&Session.Phase!=RoutePhase.NextZone;},
   TravelDialogContent.Next(CurrentTerritory.ToString(),target.TerritoryName??"Зона",target.Name??"Цель") with {Context=$"{entry.Name} · {quote} гил",Detail=TravelHint(target,entry),Footer="Сопровождение сохранится. Игра не перемещает персонажа пешком автоматически."}),true);
 }
 internal string TravelHint(MobHuntEntry target,EntryPoint? entry){
  if(entry!=null&&Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(entry.Id).Territory.RowId!=target.TerritoryType)return "Эфирит в соседнем городе. После телепорта перейдите в "+target.TerritoryName+"; метка появится после входа в зону.";
  return "После прибытия метка и сопровождение продолжатся автоматически.";
 }
 internal void OfferNext(){if(Session.Suggested is {} next)OfferTravel(next,false);}
 internal void OfferBoard(Board board){
  if(!Ready)return;uint quote=CurrentTerritory==board.Territory?0:Price(board.Aetheryte);if(quote==uint.MaxValue){NoticeText="Эфирит города не открыт.";return;}
  NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");NativeDialogs.Cancel("travel");
  string city=Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(board.Territory).PlaceName.Value.Name.ToString();
  string aetheryte=Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(board.Aetheryte).PlaceName.Value.Name.ToString();
  bool onlyNew=PickupOpen;
  NativeDialogs.Offer(new("travel",$"board:{board.Name}:{quote}:{Session.RunId}","К доске охоты?",()=>Ready&&(!onlyNew||CanPickup(board,false)||CanPickup(board,true))&&(CurrentTerritory==board.Territory||Price(board.Aetheryte)<=quote),
   ()=>!TravelBusy,()=>{if(CurrentTerritory!=board.Territory&&Price(board.Aetheryte)>quote)return false;if(!Teleport(board.Aetheryte))return false;Flow.Pauses|=PauseReason.Board;Flow.Status="Поездка к доске. Маршрут сохранён.";pendingJourney=CurrentTerritory==board.Territory?null:new(CurrentTerritory,board.Territory,board.Map,Now,null,true);PlaceFlag(board.Territory,board.Map,MapCoordinates.ToWorld(board.Position,Service.DataManager.GetExcelSheet<Map>().GetRow(board.Map)));PickupOpen=false;SaveJourney(true);return true;},
   new("HUNT ROUTE · ДОСКА",CurrentTerritory==board.Territory?"К доске без телепорта":"За новыми контрактами?","ГОРОД",city,$"{aetheryte} · {quote} гил","КОНТРАКТЫ",board.Name,"После прибытия будет поставлена метка доски.","Контракты вы берёте самостоятельно. Текущий маршрут сохраняется.",[])),true);
 }
 private unsafe string ContextKey()=> ContextProvider?.Invoke() ?? $"{Service.ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0}:{UIState.Instance()->PublicInstance.InstanceId}:{CurrentTerritory}:{CurrentMap}";
 private void ReconcileContext(){
  string current=ContextKey();if(context==null){if(Flow.Restored&&CharacterSave.Context!=null&&CharacterSave.Context!=current){CharacterSave.Search=null;CharacterSave.SearchTarget=null;}context=current;return;}if(current==context&&!externalTransition)return;
  bool expected=pendingJourney!=null;bool different=current!=context;context=current;externalTransition=false;ownedMarker=null;ordinaryActor=null;livePositions.Clear();if(different){Search.Start([]);searchKey="";}else Search.Suspend();
  if(!Flow.Active || expected || Flow.Pauses.HasFlag(PauseReason.Restored))return;
  searchKey="";
  if(Flow.Manual!=null&&Session.Target is {} manual&&manual.TerritoryType!=CurrentTerritory){Flow.Pauses|=PauseReason.NoZone;Flow.Status="Ручная цель в другой зоне. Вернитесь или отмените ручной приоритет.";return;}
  var local=Routes.SelectMany(r=>r.Mobs).FirstOrDefault(m=>m.TerritoryType==CurrentTerritory&&Eligible(m)&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual));
  if(local==null){Flow.Pauses|=PauseReason.NoZone;Flow.Status="В текущей зоне нет доступных целей. Маршрут сохранён.";}
  else{Flow.Pauses&=~(PauseReason.NoZone|PauseReason.Board|PauseReason.TravelError);Session.Continue(local);}
 }
 private void ReconcileTravel(){
  if(pendingJourney is not {} p)return;long now=Now;
  bool casting=Service.ObjectTable.LocalPlayer?.IsCasting==true;
  if(casting)p.CastingSeen=true;
  bool nearArrival=false;
  if(!casting&&p.Origin is {} origin&&p.ArrivalPoint is {} destination&&Service.ObjectTable.LocalPlayer is {} local){var here=new Vector2(local.Position.X,local.Position.Z);nearArrival=Vector2.Distance(here,origin)>80&&Vector2.Distance(here,destination)<50;}
  bool arrived=CurrentTerritory==p.Destination && (p.Source!=p.Destination || p.Loaded || nearArrival);
  if(arrived){pendingJourney=null;ownedMarker=null;Session.ZoneTransition();if(p.Board){var b=Boards.FirstOrDefault(b=>b.Territory==p.Destination);if(b!=null)PlaceFlag(b.Territory,b.Map,MapCoordinates.ToWorld(b.Position,Service.DataManager.GetExcelSheet<Map>().GetRow(b.Map)));}else{
   Flow.Pauses&=~PauseReason.TravelError;
   if(Session.Target is {} target&&target.TerritoryType!=CurrentTerritory){Flow.Pauses|=PauseReason.NoZone;Flow.Status="Телепорт завершён. Перейдите в "+target.TerritoryName+" через переход из города; сопровождение продолжится при входе.";}
  }return;}
  if(!casting && p.CastingSeen){p.CastStopped??=now;}else p.CastStopped=null;
  if(p.Expired(now) || !p.CastingSeen&&!p.Loaded&&now-p.Started>=5000 || p.CastStopped is {} stopped && now-stopped>=2000 && !p.Loaded || p.Loaded && CurrentTerritory!=p.Destination){pendingJourney=null;Flow.Pauses|=PauseReason.TravelError;Flow.Status="Телепорт отменён или прибытие не подтверждено. Нажмите «Продолжить» для повторного предложения.";externalTransition=true;}
 }
 private unsafe MarkerStamp? ReadFlag(){if(MarkerReader!=null)return MarkerReader();var map=AgentMap.Instance();if(map==null||map->FlagMarkerCount==0)return null;var flag=map->FlagMapMarkers[0];return new(flag.TerritoryId,flag.MapId,flag.XFloat,flag.YFloat);}
 private void CheckMarker(){if(ownedMarker is {} expected && Flow.CanNavigate&&pendingJourney==null&&!expected.Matches(ReadFlag())){Flow.Pauses|=PauseReason.Marker;Search.Suspend();Flow.Status="Игровая метка изменена. Нажмите «Вернуть сопровождение».";NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");NativeDialogs.Cancel("travel");}}
 private bool PlaceFlag(uint territory,uint map,Vector2 world){
  if(Flow.Active && Flow.Pauses!=PauseReason.None && !Flow.Pauses.HasFlag(PauseReason.Board))return false;
  var row=Service.DataManager.GetExcelSheet<Map>().GetRow(map);if(row.TerritoryType.RowId!=territory||!float.IsFinite(world.X)||!float.IsFinite(world.Y))return false;
  var expected=new MarkerStamp(territory,map,world.X,world.Y);
  for(int i=0;i<3;i++)if((MarkerWriter?.Invoke(expected)??Location.SetWorldFlag(territory,map,world))&&expected.Matches(ReadFlag())){ownedMarker=expected;return true;}
  Flow.Pauses|=PauseReason.FlagError;Flow.Status="Не удалось поставить метку. Нажмите «Продолжить».";return false;
 }
 internal void ReleaseLostTarget(){
  if(!Ready||Session.Target is not {IsEliteMark:true} target||Service.ObjectTable.LocalPlayer is not {} player||Service.ClientState.MapId!=target.MapId)return;
  if(Now<contractAt||Now-contractAt>1500||unreadBills.Contains(target.BillNumber)){NoticeText="Сначала дождитесь обновления данных контракта.";nextScan=0;return;}
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>n.NameId==target.MobHuntId).Select(n=>new EliteActor(n.GameObjectId,new(n.Position.X,n.Position.Z),n.IsDead)).ToArray();
  var point=Search.Observe(new(player.Position.X,player.Position.Z),actors,true,Now,Done(target),contractRevision,contractAt);
  if(Search.ReleaseLost()){Session.RetryMarker();if(Flow.CanNavigate)AdvanceRoute();}
  else if(point is {} actual&&Flow.CanNavigate)PlaceFlag(target.TerritoryType,target.MapId,actual);
 }
 internal void ShowMap(MobHuntEntry m){
  if(!Ready)return;try{
  var map=Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId);Vector2? pos=Position(m);
  if(m.IsEliteMark&&Session.Target is {} current&&JourneyFlow.Key(current)==JourneyFlow.Key(m)&&searchKey==SearchIdentity(m)&&Search.LockedPosition is {} known)pos=MapUtil.WorldToMap(known,map);
  if(pos==null){NoticeText="Для цели нет координат.";return;}
  if(Flow.Active && (Session.Target==null||JourneyFlow.Key(Session.Target)!=JourneyFlow.Key(m))){Flow.Pauses|=PauseReason.Marker;Flow.Status="Просмотр другой цели. Верните сопровождение для смены меток.";}
  Service.GameGui.OpenMapWithMapLink(MapCoordinates.CreateLink(m.TerritoryType,map,pos.Value));
  if(!Flow.Pauses.HasFlag(PauseReason.Marker)){var world=MapCoordinates.ToWorld(pos.Value,map);ownedMarker=new(m.TerritoryType,m.MapId,world.X,world.Y);}
  }catch(Exception e){NoticeText="Карту этой цели открыть не удалось: "+e.Message;}
 }
 private void RefreshLive(){
  livePositions.Clear();if(Service.ObjectTable.LocalPlayer is not {} player)return;
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>!n.IsDead&&n.GameObjectId!=0&&float.IsFinite(n.Position.X)&&float.IsFinite(n.Position.Y)&&float.IsFinite(n.Position.Z)).ToArray();
  foreach(var m in Mobs.Where(m=>!unreadBills.Contains(m.BillNumber)&&m.TerritoryType==CurrentTerritory&&m.MapId==CurrentMap&&!Done(m)&&!Flow.Excluded(m))){var npc=actors.Where(n=>n.NameId==m.MobHuntId).OrderBy(n=>Vector3.DistanceSquared(player.Position,n.Position)).FirstOrDefault();if(npc!=null)livePositions[JourneyFlow.Key(m)]=new(npc.Position.X,npc.Position.Z);}
 }
 private bool DetectorNavigation(){
  if(!Flow.Active || !Flow.CanNavigate || pendingJourney!=null || Service.ObjectTable.LocalPlayer is not {} player || Service.Condition.Any(ConditionFlag.Unconscious,ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent))return false;
  var candidates=Mobs.Where(m=>!unreadBills.Contains(m.BillNumber)&&AllTiers.Contains(m.BillNumber)&&!Done(m)&&!Flow.Excluded(m)&&m.TerritoryType==CurrentTerritory&&m.MapId==CurrentMap).ToArray();
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>!n.IsDead&&n.GameObjectId!=0&&float.IsFinite(n.Position.X)&&float.IsFinite(n.Position.Y)&&float.IsFinite(n.Position.Z) && candidates.Any(m=>m.MobHuntId==n.NameId)).ToArray();
  livePositions.Clear();foreach(var m in candidates){var near=actors.Where(n=>n.NameId==m.MobHuntId).OrderBy(n=>Vector3.DistanceSquared(player.Position,n.Position)).FirstOrDefault();if(near!=null)livePositions[JourneyFlow.Key(m)]=new(near.Position.X,near.Position.Z);}
  var elite=candidates.Where(m=>m.IsEliteMark&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual)).SelectMany(m=>actors.Where(n=>n.NameId==m.MobHuntId).Select(n=>(Mob:m,Npc:n))).OrderBy(p=>Vector3.DistanceSquared(player.Position,p.Npc.Position)).FirstOrDefault();
  if(elite.Mob!=null && (Session.Target==null||JourneyFlow.Key(Session.Target)!=JourneyFlow.Key(elite.Mob))){Session.Retarget(elite.Mob);ordinaryActor=null;}
  if(Session.Target is {IsEliteMark:true})return false;
  var ordinary=actors.Where(n=>candidates.Any(m=>!m.IsEliteMark&&m.MobHuntId==n.NameId&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual))).OrderBy(n=>n.GameObjectId==ordinaryActor?0:1).ThenBy(n=>Vector3.DistanceSquared(player.Position,n.Position)).ThenBy(n=>n.GameObjectId).FirstOrDefault();
  if(ordinary==null){if(ordinaryActor!=null){ordinaryActor=null;ordinaryKind=null;Session.RetryMarker();}return false;}
  var target=candidates.First(m=>!m.IsEliteMark&&m.MobHuntId==ordinary.NameId&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual));
  if(Session.Target==null || JourneyFlow.Key(Session.Target)!=JourneyFlow.Key(target))Session.Retarget(target);
  var position=new Vector2(ordinary.Position.X,ordinary.Position.Z);
  bool changed=ordinaryActor!=ordinary.GameObjectId||ordinaryKind!=JourneyFlow.Key(target)||ordinaryPosition is not {} previousPosition||Vector2.Distance(previousPosition,position)>=2;
  ordinaryActor=ordinary.GameObjectId;ordinaryKind=JourneyFlow.Key(target);ordinaryPosition=position;
  if(changed)PlaceFlag(target.TerritoryType,target.MapId,position);
  return true;
 }
 private unsafe void Ping(string text,bool sound=false){NoticeText=text;if(sound&&Settings.Sound){try{FFXIVClientStructs.FFXIV.Client.UI.UIGlobals.PlaySoundEffect(0x29);}catch(Exception e){Service.PluginLog.Debug(e,"Сигнал охоты недоступен");}}}
 private void AuditState(){string value=$"{Session.Phase}|{Session.Target?.MobHuntId}|{Flow.Pauses}|{Flow.Status}";if(value==lastJournal)return;lastJournal=value;journal.Enqueue(DateTimeOffset.UtcNow.ToString("O")+" "+value);while(journal.Count>200)journal.Dequeue();}
 internal void Export(){if(stateFile==null)return;string path=Path.Combine(Path.GetDirectoryName(stateFile)!,"hunt-diagnostics.txt");try{File.WriteAllLines(path,new[]{"Hunt Route "+typeof(Plugin).Assembly.GetName().Version}.Concat(journal));NoticeText="Диагностика сохранена: "+path;}catch(Exception e){NoticeText="Не удалось сохранить диагностику: "+e.Message;}}
 internal bool PersistenceBlocked=>stateLoadPending;
 internal bool CompletionUncertain(Board board,bool weekly)=>(completionLoadPending||History.RequiresPickupEvidence)&&(weekly?new[]{board.Weekly}:board.Daily).Any(i=>BillStates.TryGetValue(i,out var s)&&s.CompletedCurrentSet&&!History.IsComplete(character,i,weekly,DateTimeOffset.UtcNow,s.AvailableId));
 internal bool BoardReachable(Board b)=>CurrentTerritory==b.Territory||Service.Aetherytes is not null&&Service.Aetherytes.Any(a=>a.AetheryteId==b.Aetheryte);
 internal string LocationLabel=>Session.Target is {IsEliteMark:true}?Search.Lost?"Последняя известная позиция":Search.ActorId!=null?"Элита обнаружена":"Предполагаемые места появления":ordinaryActor!=null&&Session.Target is {} goal&&ordinaryKind==JourneyFlow.Key(goal)?"Моб обнаружен":"Известная точка охоты";
 internal bool CanPickup(Board b,bool weekly){var ids=weekly?new[]{b.Weekly}:b.Daily;return ids.Any(i=>!unreadBills.Contains(i)&&BillStates.TryGetValue(i,out var state)&&state.Unlocked&&state.AvailableId>0&&!state.Taken&&!state.CompletedCurrentSet&&!History.IsComplete(character,i,weekly,DateTimeOffset.UtcNow,state.AvailableId));}
 private void CheckResetNotice(){
  if(!Settings.ResetNotifications||!Ready||contractRevision<2)return;
  var now=DateTimeOffset.UtcNow;long daily=ResetClock.Period(now,false).ToUnixTimeSeconds(),weekly=ResetClock.Period(now,true).ToUnixTimeSeconds();
  bool d=CharacterSave.DailyNotice!=daily&&Boards.Any(b=>CanPickup(b,false));bool w=CharacterSave.WeeklyNotice!=weekly&&Boards.Any(b=>CanPickup(b,true));
  if(!d&&!w)return;
  if(Flow.Active&&Flow.Pauses==PauseReason.None){NoticeText="Доступны новые контракты — выберите доску в сводке.";return;}
  if(TravelBusy || pendingJourney!=null || Flow.Pauses.HasFlag(PauseReason.Marker))return;
  NativeDialogs.Offer(new("pickup",$"pickup:{character}:{(d?daily:0)}:{(w?weekly:0)}","Доступны новые контракты",()=>Ready&&Settings.ResetNotifications&&(d&&ResetClock.Period(DateTimeOffset.UtcNow,false).ToUnixTimeSeconds()==daily&&Boards.Any(b=>CanPickup(b,false))||w&&ResetClock.Period(DateTimeOffset.UtcNow,true).ToUnixTimeSeconds()==weekly&&Boards.Any(b=>CanPickup(b,true))),()=>!TravelBusy&&pendingJourney==null&&(!Flow.Active||Flow.Pauses!=PauseReason.None)&&!Flow.Pauses.HasFlag(PauseReason.Marker),()=>{PickupOpen=true;window.IsOpen=true;return true;},new("HUNT ROUTE · КОНТРАКТЫ","Доступны новые контракты","ДОСКА ОХОТЫ","Выберите дополнение","Ежедневные и недельные статусы показаны отдельно.","ПОСЛЕ ВЫБОРА","Подтверждение поездки","Контракты вы берёте самостоятельно.","Текущий маршрут сохранится. «Позже» оставит действие в сводке.",[],"Выбрать дополнение"),()=>{if(d)CharacterSave.DailyNotice=daily;if(w)CharacterSave.WeeklyNotice=weekly;SaveJourney(true);}));
 }
}
