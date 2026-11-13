using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using Dalamud.Game.Text.SeStringHandling;
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
 private bool loginNoticeShown;
 private HuntStore? store;
 internal HuntStore Store=>store??=new();
 private JourneyFlow? flow;
 internal JourneyFlow Flow=>flow??=new();
 private HuntSettingsFile? settingsStore;
 private string? settingsFile,lastSettingsSaved;
 private bool settingsLoadPending;
 private long nextSettingsSave;
 internal string? JourneySaveError,SettingsSaveError,HistorySaveError;
 internal HuntSettings Settings=>settingsStore?.Settings??Store.Settings;
 private string? stateFile,lastSaved,context;
 private long nextStateSave,contractRevision,contractAt;
 private PendingJourney? pendingJourney;
 private MarkerStamp? ownedMarker;
 private byte? ownedMarkerBill;
 internal System.Action? MarkerClearer {get;set;}
 private ulong? ordinaryActor;
 private Vector2? ordinaryPosition;
 private float ordinaryHeight;
 private readonly Dictionary<string,float> shortcutHeights=[];
 private EliteProgress? restoreSearch;
 private Dictionary<string,Vector2>? live;
 private Dictionary<string,Vector2> livePositions=>live??=new();
 private string? ordinaryKind;
 private bool externalTransition;
 internal string? Selected;
 internal bool FocusRequested;
 internal void FocusTarget(){if(Session.Target!=null)Selected=JourneyFlow.Key(Session.Target);if(NativeMain?.Available==true)NativeMain.FocusTarget();else{FocusRequested=true;if(window!=null)window.IsOpen=true;}}
 private string? noticeText,noticeTarget;
 internal string? NoticeText {
  get=>noticeTarget==null||Flow.Active&&Session.Phase==RoutePhase.Following
   &&Session.Target is {IsEliteMark:true} target&&noticeTarget==SearchIdentity(target)&&!Done(target)?noticeText:null;
  set{noticeText=value;noticeTarget=null;}
 }
 internal bool PickupOpen,SettingsOpen,DeferredOpen;
 private readonly Queue<string> journal=new();
 private string? lastJournal;
 internal SavedJourney CharacterSave {
  get {if(!Store.Characters.TryGetValue(character,out var s))Store.Characters[character]=s=new();return s;}
 }
 private void InitializeStore(){
  stateFile=Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(),"journey-state.json");
  settingsFile=Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(),"hunt-settings.json");
  try{store=HuntStore.Load(stateFile);}catch(Exception e){stateLoadPending=true;store=new();Service.PluginLog.Error(e,"Не удалось прочитать состояние маршрута");NoticeText="Сохранение маршрута недоступно; история контрактов хранится отдельно.";}
  if(stateLoadPending&&!File.Exists(settingsFile)){
   settingsLoadPending=true;
   SettingsSaveError="Настройки ожидают чтения прежнего сохранения.";
   return;
  }
  try{settingsStore=HuntSettingsFile.Load(settingsFile,Store.Settings);if(settingsStore.Recovered)NoticeText="Настройки восстановлены; история выполнения и маршруты хранятся отдельно.";}catch(Exception e){settingsLoadPending=true;SettingsSaveError="Настройки временно недоступны. Исходный файл сохранён.";Service.PluginLog.Error(e,"Чтение настроек");}
 }
 private bool RecoverJourneyStore(){
  if(!stateLoadPending)return true;
  try{store=HuntStore.Load(stateFile!);}
  catch(Exception e){
   nextScan=Now+10000;
   JourneySaveError="Не удалось прочитать сохранение маршрута. Повторим чтение.";
   Service.PluginLog.Debug(e,"Повторное чтение маршрута");
   return false;
  }
  stateLoadPending=false;
  JourneySaveError=null;
  signature=routeSignature=lastSaved="";
  Session.Stop();
  LoadJourney();
  return true;
 }
 private readonly ExpansionCompletionNotice expansionCompletionNotice=new();
 private void LoadJourney(){
  expansionCompletionNotice.Reset();
  if(noticeTarget!=null)NoticeText=null;
  loginNoticeShown=false;
  billGuard=new();uncertainBills.Clear();unreadBills.Clear();KillCounts=new();BillStates=new();Mobs=new();Routes=new();CompletedGoals=[];completedGoalCache?.Clear();
  contractRevision=0;contractAt=0;flow=new();Flow.Load(CharacterSave);ordinaryActor=null;ordinaryKind=null;context=null;ownedMarker=null;pendingJourney=null;
  if(Flow.Active)Flow.Status="Сохранённый маршрут на паузе. Проверьте контракты и нажмите «Продолжить».";
 }
 internal bool AcceptBillSnapshot(byte bill,BillSnapshot snapshot,byte maximum){
  var episodes=CharacterSave.Contracts;if(!episodes.TryGetValue(bill,out var episode))episodes[bill]=episode=new();
  if(!Guard.Observe(bill,snapshot,Now,maximum,episode.Taken,episode.Order)){uncertainBills.Add(bill);unreadBills.Add(bill);return false;}
  uncertainBills.Remove(bill);
  if(snapshot.Taken&&(!episode.Taken||episode.Order!=snapshot.Order))episode.Generation++;
  episode.Taken=snapshot.Taken;if(snapshot.Order>0)episode.Order=snapshot.Order;
  for(byte mob=0;mob<5;mob++)KillCounts[(bill,mob)]=snapshot.Kills[mob];
  return true;
 }
 internal void SaveJourney(bool force=false){
  SaveSettings(force);
  if(stateLoadPending || stateFile==null || character==0 || (!force && Now<nextStateSave))return;
  nextStateSave=Now+2000;
  var s=CharacterSave;s.Context=context;s.Active=Flow.Active;s.Manual=Flow.Manual;s.Target=Session.Target is {} t?JourneyFlow.Key(t):Flow.RestoreTarget;
  s.Deferred=Flow.Deferred.ToHashSet();s.Zones=Flow.Zones.ToHashSet();s.LongNotified=Flow.LongNotified.ToHashSet();
  if(!Flow.Restored){s.SearchTarget=searchKey;s.Search=Session.Target is {IsEliteMark:true} currentSearch&&searchKey==SearchIdentity(currentSearch)?Search.Capture():null;}
  string value=JsonSerializer.Serialize(Store,new JsonSerializerOptions{IncludeFields=true});
  if(!force && value==lastSaved&&JourneySaveError==null)return;
  try{Store.Save(stateFile);lastSaved=value;nextStateSave=Now+2000;JourneySaveError=null;}catch(Exception e){nextStateSave=Now+10000;JourneySaveError="Не удалось сохранить маршрут: после перезапуска изменения могут пропасть.";Service.PluginLog.Error(e,"Сохранение маршрута");}
 }
 private void SaveSettings(bool force=false){
  if(settingsFile==null||stateLoadPending&&settingsLoadPending||!force&&Now<nextSettingsSave)return;
  if(settingsLoadPending)try{settingsStore=HuntSettingsFile.Load(settingsFile,Store.Settings);settingsLoadPending=false;}catch(Exception e){nextSettingsSave=Now+10000;SettingsSaveError="Настройки недоступны для чтения. Исходный файл не перезаписан.";Service.PluginLog.Debug(e,"Повторное чтение настроек");return;}
  nextSettingsSave=Now+2000;
  settingsStore??=new(){Settings=Store.Settings};string value=JsonSerializer.Serialize(settingsStore);
  if(!force&&lastSettingsSaved==value&&SettingsSaveError==null)return;
  try{settingsStore.Save(settingsFile);Store.PreserveLegacySettings=false;lastSettingsSaved=value;nextSettingsSave=Now+2000;SettingsSaveError=null;}catch(Exception e){nextSettingsSave=Now+10000;SettingsSaveError="Не удалось сохранить настройки: изменения могут пропасть после перезапуска.";Service.PluginLog.Error(e,"Сохранение настроек");}
 }
 internal void RetryPersistence(){SaveJourney(true);SaveHistory(true);if(JourneySaveError==null&&SettingsSaveError==null&&HistorySaveError==null)NoticeText="Данные сохранены.";}
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
 private MobHuntEntry? ManualTarget()=>Flow.Manual is {} key?Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==key&&!Done(m)&&!Flow.Excluded(m)):null;
 private void PauseUnavailableManual(){
  Flow.Pauses|=PauseReason.Unavailable;
  Flow.Status="Ручная цель временно недоступна. Выбор сохранён.";
  Search.Suspend();NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");
 }
 private void ResetTargetTracking(){Search.Start([]);searchKey="";ordinaryActor=null;ordinaryKind=null;ordinaryPosition=null;}
 private string SearchIdentity(MobHuntEntry target)=>$"{Session.RunId}:{JourneyFlow.Key(target)}";
 internal bool NavigationAllowed=>(!Flow.Active || Flow.Pauses==PauseReason.None)&&!(Session.Target is {} t&&unreadBills.Contains(t.BillNumber));
 internal void StopJourney(){Flow.Stop();Session.Stop();Search.Start([]);searchKey="";pendingJourney=null;ordinaryActor=null;ownedMarker=null;NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");NativeDialogs.Cancel("travel");SaveJourney(true);}
 internal void TogglePause(){if(!Flow.Active)return;if(Flow.Pauses.HasFlag(PauseReason.User))ResumeJourney();else{Flow.Pauses|=PauseReason.User;Search.Suspend();Flow.Status="Пауза по вашему выбору";SaveJourney(true);}}
 internal void ResumeJourney(){
  if(!Ready)return;
  if(pendingJourney!=null){NoticeText="Текущее перемещение ещё не завершено. Маршрут будет обновлён после прибытия.";return;}
  RefreshLive();
  Flow.Pauses&=~(PauseReason.User|PauseReason.Restored|PauseReason.Marker|PauseReason.TravelError|PauseReason.Unavailable|PauseReason.FlagError|PauseReason.Board|PauseReason.NoZone);
  ownedMarker=null;Search.Suspend();
  var target=Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==(Flow.Manual??Flow.RestoreTarget)&&Eligible(m));
  if(target==null&&ManualTarget()!=null){PauseUnavailableManual();SaveJourney(true);return;}
  if(target==null && Flow.Manual!=null){Flow.Manual=null;Flow.Status="Ручная цель больше не активна.";}
  target??=Session.Target is {} old?Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==JourneyFlow.Key(old)&&Eligible(m)):null;
  target??=BestTarget();
  if(target==null){Flow.Status="Нет доступных целей. Проверьте отложенные контракты.";NoticeText=Flow.Status;SaveJourney(true);return;}
  bool starting=!Flow.Active || Session.Phase is RoutePhase.Idle or RoutePhase.Finished or RoutePhase.Changed;
  if(Flow.Restored && CharacterSave.Target==JourneyFlow.Key(target))restoreSearch=CharacterSave.Search;
  Flow.Active=true;Flow.Restored=false;Flow.RestoreTarget=null;
  if(CurrentTerritory==target.TerritoryType){if(starting)Session.Start(target,AllTiers);else Session.Continue(target);Session.RetryMarker();Search.Retry();AdvanceRoute();}
  else OfferTravel(target,starting);
  SaveJourney(true);
 }
 private Vector2[] PlanningPoints(MobHuntEntry m){
  if(unreadBills.Contains(m.BillNumber)||Done(m)||Flow.Excluded(m))return [];
  var map=Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId);if(map.SizeFactor==0||map.TerritoryType.RowId!=m.TerritoryType)return [];
  if(livePositions.TryGetValue(JourneyFlow.Key(m),out var livePoint))return [livePoint];
  if(m.IsEliteMark)return EliteSpawns.For(m).Select(p=>MapCoordinates.ToWorld(p,map)).ToArray();
  return Location.Database.TryGetValue(m.MobHuntId,out var anchor)?[MapCoordinates.ToWorld(anchor.Coordinate,map)]:[];
 }
 internal void SetMini(bool enabled){Settings.Mini=enabled;if(miniWindow!=null)miniWindow.IsOpen=enabled;SaveJourney(true);}
 internal void NextOffered(){if(Flow.Active&&Settings.NextZoneSound)PlayHuntSound();}
 internal MobHuntEntry? BestTarget()=>Routes.SelectMany(r=>r.Mobs).Where(Eligible).OrderBy(m=>m.TerritoryType==CurrentTerritory?0:1).FirstOrDefault();
 internal void Track(MobHuntEntry m){
  if(!Ready||Done(m)||Flow.Excluded(m))return;
  if(pendingJourney!=null){NoticeText="Дождитесь завершения текущего перемещения, затем выберите цель.";return;}
  RefreshLive();if(!HasPosition(m)){NoticeText="Для этой цели пока нет доступной позиции. Она остаётся незавершённой.";return;}
  EntryPoint? entry=null;
  if(CurrentTerritory!=m.TerritoryType&&(entry=EntryFor(m))==null){
   Flow.Unavailable[JourneyFlow.Key(m)]="Нет открытого эфирита или проверенного входа";
   NoticeText="Цель пока недоступна: откройте эфирит и выберите её снова.";
   return;
  }
  if(Session.Target is {} prior&&JourneyFlow.Key(prior)!=JourneyFlow.Key(m))ResetTargetTracking();
  Flow.Manual=JourneyFlow.Key(m);Flow.Active=true;Flow.Pauses&=~(PauseReason.User|PauseReason.Restored|PauseReason.TravelError|PauseReason.NoZone|PauseReason.Board);
  if(CurrentTerritory==m.TerritoryType){if(Session.Phase is RoutePhase.Idle or RoutePhase.Finished)Session.Start(m,AllTiers);else Session.Continue(m);AdvanceRoute();}
  else OfferTravel(m,Session.Phase==RoutePhase.Idle,entry);
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
 internal EntryPoint? EntryFor(MobHuntEntry m){
  var route=Routes.FirstOrDefault(r=>r.Mobs.Any(t=>JourneyFlow.Key(t)==JourneyFlow.Key(m)));
  if(route?.Start is {} planned&&route.Mobs.FirstOrDefault(t=>!Done(t)&&!Flow.Excluded(t)&&PlanningPoints(t).Length>0) is {} first&&JourneyFlow.Key(first)==JourneyFlow.Key(m)&&IsAetheryteUnlocked(planned.Id))return planned;
  return NearestEntryFor(m);
 }
 private EntryPoint? NearestEntryFor(MobHuntEntry m){var positions=m.IsEliteMark?EliteSpawns.For(m):Position(m) is {} p?new[]{p}:Array.Empty<Vector2>();return Entries(m.MapId,m.TerritoryType).OrderBy(e=>Price(e.Id)).ThenBy(e=>positions.Length==0?float.MaxValue:positions.Min(p=>Vector2.DistanceSquared(e.Position,p))).ThenBy(e=>e.Id).FirstOrDefault() ?? (m.TerritoryType==399&&IsAetheryteUnlocked(75)?new(75,"Idyllshire",Vector2.Zero):null);}
 internal uint Price(uint id)=>fareSnapshot?.GetValueOrDefault(id,uint.MaxValue)??Service.Aetherytes.Where(a=>a.AetheryteId==id).Select(a=>a.GilCost).DefaultIfEmpty(uint.MaxValue).Min();
 private void OfferTravel(MobHuntEntry target,bool starting,EntryPoint? reviewedEntry=null){
  if(Service.ClientState.TerritoryType==target.TerritoryType){
   NativeDialogs.Cancel("travel");NativeDialogs.Cancel("next");
   if(Session.Target is {} previous&&JourneyFlow.Key(previous)!=JourneyFlow.Key(target))ResetTargetTracking();
   Flow.Active=true;
   if(starting)Session.Start(target,AllTiers);else Session.Continue(target);
   AdvanceRoute();return;
  }
  var entry=reviewedEntry??EntryFor(target);if(entry==null){Flow.Unavailable[JourneyFlow.Key(target)]="Нет открытого эфирита или проверенного входа";Flow.Status="Цель отложена: нет открытого входа.";return;}
  if(Session.Target is {} old&&JourneyFlow.Key(old)!=JourneyFlow.Key(target))ResetTargetTracking();
  Session.PlanTravel(target,AllTiers,starting);
  string key=JourneyFlow.Key(target);uint run=Session.RunId;uint quote=Price(entry.Id);
  NativeDialogs.Offer(new("travel",$"travel:{run}:{key}:{entry.Id}:{quote}","Продолжить охоту?",
   ()=>Ready && Service.ClientState.TerritoryType!=target.TerritoryType&&Eligible(target)&&Flow.Active&&Session.RunId==run&&Session.Phase==RoutePhase.AwaitingTravel&&Session.Target is {} planned&&JourneyFlow.Key(planned)==key&&Mobs.Any(m=>JourneyFlow.Key(m)==key)&&EntryFor(target)?.Id==entry.Id&&Price(entry.Id)<=quote,
   ()=>!TravelBusy && NavigationAllowed,
   ()=>{var current=EntryFor(target);if(current?.Id!=entry.Id || Price(entry.Id)>quote)return false;Travel(target);return Session.Target is {} t&&JourneyFlow.Key(t)==key&&Session.Phase is RoutePhase.WaitingForZone or RoutePhase.Following;},
   TravelDialogContent.Next(CurrentTerritory.ToString(),target.TerritoryName??"Зона",target.Name??"Цель",target.Icon) with {Context=entry.Name,Fare=quote,Detail=TravelHint(target,entry)}),true);
 }
 internal string TravelHint(MobHuntEntry target,EntryPoint? entry){
  if(entry!=null&&Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(entry.Id).Territory.RowId!=target.TerritoryType)return "После телепорта перейдите в зону цели.";
  return "";
 }
 internal void OfferNext(){if(Session.Phase==RoutePhase.AwaitingTravel)OfferPlannedTravel();else if(Session.Suggested is {} next)OfferTravel(next,false);}
 internal void OfferPlannedTravel(){if(Session.Phase==RoutePhase.AwaitingTravel&&Session.Target is {} target)OfferTravel(target,false);}
 internal void OfferBoard(Board board){
  if(!Ready)return;
  if(pendingJourney!=null){NoticeText="Дождитесь завершения текущего перемещения.";return;}
  uint quote=CurrentTerritory==board.Territory?0:Price(board.Aetheryte);if(quote==uint.MaxValue){NoticeText="Эфирит города не открыт.";return;}
  NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");NativeDialogs.Cancel("travel");
  string city=Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(board.Territory).PlaceName.Value.Name.ToString();
  string aetheryte=Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(board.Aetheryte).PlaceName.Value.Name.ToString();
  bool onlyNew=PickupOpen;
  NativeDialogs.Offer(new("travel",$"board:{board.Name}:{quote}:{Session.RunId}","К доске охоты?",()=>Ready&&(!onlyNew||CanPickup(board,false)||CanPickup(board,true))&&(CurrentTerritory==board.Territory||Price(board.Aetheryte)<=quote),
   ()=>!TravelBusy&&pendingJourney==null,()=>{if(pendingJourney!=null)return false;if(CurrentTerritory!=board.Territory&&Price(board.Aetheryte)>quote)return false;if(!Teleport(board.Aetheryte))return false;Flow.Pauses|=PauseReason.Board;Flow.Status="Поездка к доске. Маршрут сохранён.";pendingJourney=CurrentTerritory==board.Territory?null:new(CurrentTerritory,board.Territory,board.Map,Now,null,true);PlaceFlag(board.Territory,board.Map,MapCoordinates.ToWorld(board.Position,Service.DataManager.GetExcelSheet<Map>().GetRow(board.Map)));PickupOpen=false;SaveJourney(true);return true;},
   TravelDialogContent.Board(city,aetheryte,board.Name,quote,CurrentTerritory==board.Territory)),true);
 }
 private unsafe string ContextKey()=> ContextProvider?.Invoke() ?? $"{Service.ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0}:{UIState.Instance()->PublicInstance.InstanceId}:{CurrentTerritory}:{CurrentMap}";
 private void ReconcileContext(){
  string current=ContextKey();if(context==null){if(Flow.Restored&&CharacterSave.Context!=null&&CharacterSave.Context!=current){CharacterSave.Search=null;CharacterSave.SearchTarget=null;}context=current;return;}if(current==context&&!externalTransition)return;
  bool expected=pendingJourney!=null;bool different=current!=context;context=current;externalTransition=false;ownedMarker=null;ordinaryActor=null;livePositions.Clear();shortcutHeights.Clear();if(different){Search.Start([]);searchKey="";}else Search.Suspend();
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
  bool wrongContext=p.Source==p.Destination && (CurrentMap!=p.Map||p.OriginContext!=null&&p.OriginContext!=ContextKey());
  bool sameZone=p.Source==p.Destination;
  bool arrived=CurrentTerritory==p.Destination && (!sameZone || !wrongContext&&nearArrival&&(p.Loaded||p.CastingSeen));
  if(arrived){pendingJourney=null;ownedMarker=null;Session.ZoneTransition();if(p.Board){var b=Boards.FirstOrDefault(b=>b.Territory==p.Destination);if(b!=null)PlaceFlag(b.Territory,b.Map,MapCoordinates.ToWorld(b.Position,Service.DataManager.GetExcelSheet<Map>().GetRow(b.Map)));}else{
   Flow.Pauses&=~PauseReason.TravelError;
   if(Session.Target is {} target&&target.TerritoryType!=CurrentTerritory){Flow.Pauses|=PauseReason.NoZone;Flow.Status="Телепорт завершён. Перейдите в "+target.TerritoryName+" через переход из города; сопровождение продолжится при входе.";}
  }return;}
  if(!casting && p.CastingSeen){p.CastStopped??=now;}else p.CastStopped=null;
  if(sameZone&&p.Loaded&&!casting)p.LoadingFinishedAt??=now;
  if(p.Expired(now) || p.LoadingFinishedAt is {} finished&&now-finished>=5000 || !p.CastingSeen&&!p.Loaded&&now-p.Started>=5000 || p.CastStopped is {} stopped && now-stopped>=2000 && !p.Loaded || p.Loaded && (CurrentTerritory!=p.Destination||wrongContext)){pendingJourney=null;Timing.Reset();Flow.Pauses|=PauseReason.TravelError;Flow.Status="Телепорт отменён или прибытие не подтверждено. Нажмите «Продолжить» для повторного предложения.";}
 }
 private unsafe MarkerStamp? ReadFlag(){if(MarkerReader!=null)return MarkerReader();var map=AgentMap.Instance();if(map==null||map->FlagMarkerCount==0)return null;var flag=map->FlagMapMarkers[0];return new(flag.TerritoryId,flag.MapId,flag.XFloat,flag.YFloat);}
 private void CheckMarker(){if(ownedMarker is {} expected && Flow.CanNavigate&&pendingJourney==null&&!expected.Matches(ReadFlag())){Flow.Pauses|=PauseReason.Marker;Search.Suspend();Flow.Status="Игровая метка изменена. Нажмите «Вернуть сопровождение».";NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");NativeDialogs.Cancel("travel");}}
 private bool PlaceFlag(uint territory,uint map,Vector2 world,byte? bill=null){
  if(Flow.Active && Flow.Pauses!=PauseReason.None && !Flow.Pauses.HasFlag(PauseReason.Board))return false;
  var row=Service.DataManager.GetExcelSheet<Map>().GetRow(map);if(row.TerritoryType.RowId!=territory||!float.IsFinite(world.X)||!float.IsFinite(world.Y))return false;
  var expected=new MarkerStamp(territory,map,world.X,world.Y);
  for(int i=0;i<3;i++)if((MarkerWriter?.Invoke(expected)??Location.SetWorldFlag(territory,map,world))&&expected.Matches(ReadFlag())){ownedMarker=expected;ownedMarkerBill=bill;return true;}
  Flow.Pauses|=PauseReason.FlagError;Flow.Status="Не удалось поставить метку. Нажмите «Продолжить».";return false;
 }
 internal void ReleaseLostTarget(){
  if(!Ready||Session.Target is not {IsEliteMark:true} target||Service.ObjectTable.LocalPlayer is not {} player||Service.ClientState.MapId!=target.MapId)return;
  if(Now<contractAt||Now-contractAt>1500||unreadBills.Contains(target.BillNumber)){NoticeText="Сначала дождитесь обновления данных контракта.";nextScan=0;return;}
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>n.NameId==target.MobHuntId).Select(n=>new EliteActor(n.GameObjectId,new(n.Position.X,n.Position.Z),n.IsDead)).ToArray();
  var point=Search.Observe(new(player.Position.X,player.Position.Z),actors,true,Now,Done(target),contractRevision,contractAt);
  if(Search.ReleaseLost()){Session.RetryMarker();if(Flow.CanNavigate)AdvanceRoute();}
  else if(point is {} actual&&Flow.CanNavigate)PlaceFlag(target.TerritoryType,target.MapId,actual,target.BillNumber);
 }
 internal void ShowMap(MobHuntEntry m){
  if(!Ready)return;try{
  var map=Service.DataManager.GetExcelSheet<Map>().GetRow(m.MapId);Vector2? pos=Position(m);
  bool current=Session.Target is {} target&&JourneyFlow.Key(target)==JourneyFlow.Key(m);
  if(current){
   if(m.IsEliteMark&&searchKey==SearchIdentity(m)&&Search.LockedPosition is {} known)pos=MapUtil.WorldToMap(known,map);
   else if(!m.IsEliteMark&&ordinaryActor!=null&&ordinaryKind==JourneyFlow.Key(m)&&ordinaryPosition is {} tracked)pos=MapUtil.WorldToMap(tracked,map);
  }
  if(pos==null){NoticeText="Для цели нет координат.";return;}
  if(!Service.GameGui.OpenMapWithMapLink(MapCoordinates.CreateLink(m.TerritoryType,map,pos.Value))){NoticeText="Карту этой цели открыть не удалось.";return;}
  if(Flow.Active && !current){Flow.Pauses|=PauseReason.Marker;Flow.Status="Просмотр другой цели. Верните сопровождение для смены меток.";}
  if(!Flow.Pauses.HasFlag(PauseReason.Marker)){var world=MapCoordinates.ToWorld(pos.Value,map);ownedMarker=new(m.TerritoryType,m.MapId,world.X,world.Y);ownedMarkerBill=m.BillNumber;}
  }catch(Exception e){NoticeText="Карту этой цели открыть не удалось: "+e.Message;}
 }
 private void RefreshLive(){
  livePositions.Clear();shortcutHeights.Clear();if(Service.ObjectTable.LocalPlayer is not {} player)return;
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>!n.IsDead&&n.GameObjectId!=0&&float.IsFinite(n.Position.X)&&float.IsFinite(n.Position.Y)&&float.IsFinite(n.Position.Z)).ToArray();
  foreach(var m in Mobs.Where(m=>!unreadBills.Contains(m.BillNumber)&&m.TerritoryType==CurrentTerritory&&m.MapId==CurrentMap&&!Done(m)&&!Flow.Excluded(m))){var npc=actors.Where(n=>n.NameId==m.MobHuntId).OrderBy(n=>Vector3.DistanceSquared(player.Position,n.Position)).FirstOrDefault();if(npc!=null){livePositions[JourneyFlow.Key(m)]=new(npc.Position.X,npc.Position.Z);shortcutHeights[JourneyFlow.Key(m)]=npc.Position.Y;}}
 }
 private bool DetectorNavigation(){
  if(!Flow.Active || !Flow.CanNavigate || !NavigationAllowed || pendingJourney!=null || Service.ObjectTable.LocalPlayer is not {} player || Service.Condition.Any(ConditionFlag.Unconscious,ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent))return false;
  var candidates=Mobs.Where(m=>!unreadBills.Contains(m.BillNumber)&&AllTiers.Contains(m.BillNumber)&&!Done(m)&&!Flow.Excluded(m)&&m.TerritoryType==CurrentTerritory&&m.MapId==CurrentMap).ToArray();
  var actors=Service.ObjectTable.OfType<IBattleNpc>().Where(n=>!n.IsDead&&n.GameObjectId!=0&&float.IsFinite(n.Position.X)&&float.IsFinite(n.Position.Y)&&float.IsFinite(n.Position.Z) && candidates.Any(m=>m.MobHuntId==n.NameId)).ToArray();
  livePositions.Clear();shortcutHeights.Clear();foreach(var m in candidates){var near=actors.Where(n=>n.NameId==m.MobHuntId).OrderBy(n=>Vector3.DistanceSquared(player.Position,n.Position)).FirstOrDefault();if(near!=null){livePositions[JourneyFlow.Key(m)]=new(near.Position.X,near.Position.Z);shortcutHeights[JourneyFlow.Key(m)]=near.Position.Y;}}
  var elite=candidates.Where(m=>m.IsEliteMark&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual)).SelectMany(m=>actors.Where(n=>n.NameId==m.MobHuntId).Select(n=>(Mob:m,Npc:n))).OrderBy(p=>Vector3.DistanceSquared(player.Position,p.Npc.Position)).FirstOrDefault();
  if(elite.Mob!=null && (Session.Target==null||JourneyFlow.Key(Session.Target)!=JourneyFlow.Key(elite.Mob))){Session.Retarget(elite.Mob);ordinaryActor=null;}
  if(Session.Target is {IsEliteMark:true})return false;
  var ordinary=actors.Where(n=>candidates.Any(m=>!m.IsEliteMark&&m.MobHuntId==n.NameId&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual))).OrderBy(n=>n.GameObjectId==ordinaryActor?0:1).ThenBy(n=>Vector3.DistanceSquared(player.Position,n.Position)).ThenBy(n=>n.GameObjectId).FirstOrDefault();
  if(ordinary==null){if(ordinaryActor!=null){ordinaryActor=null;ordinaryKind=null;Session.RetryMarker();}return false;}
  var target=candidates.First(m=>!m.IsEliteMark&&m.MobHuntId==ordinary.NameId&&(Flow.Manual==null||JourneyFlow.Key(m)==Flow.Manual));
  if(Session.Target==null || JourneyFlow.Key(Session.Target)!=JourneyFlow.Key(target))Session.Retarget(target);
  var position=new Vector2(ordinary.Position.X,ordinary.Position.Z);
  bool changed=ordinaryActor!=ordinary.GameObjectId||ordinaryKind!=JourneyFlow.Key(target)
   ||ownedMarker is not {} flag||flag.Territory!=target.TerritoryType||flag.Map!=target.MapId||Vector2.Distance(new(flag.X,flag.Y),position)>=2;
  ordinaryActor=ordinary.GameObjectId;ordinaryKind=JourneyFlow.Key(target);ordinaryPosition=position;ordinaryHeight=ordinary.Position.Y;
  if(changed)PlaceFlag(target.TerritoryType,target.MapId,position,target.BillNumber);
  return true;
 }
 private void Ping(string text,bool sound=false,MobHuntEntry? target=null){NoticeText=text;if(target!=null)noticeTarget=SearchIdentity(target);if(sound)PlayHuntSound();}
 internal static unsafe void PlayHuntSound(){try{FFXIVClientStructs.FFXIV.Client.UI.UIGlobals.PlaySoundEffect(0x29);}catch(Exception e){Service.PluginLog.Debug(e,"Сигнал охоты недоступен");}}
 private void AuditState(){string value=$"{Session.Phase}|{Session.Target?.MobHuntId}|{Flow.Pauses}|{Flow.Status}";if(value==lastJournal)return;lastJournal=value;journal.Enqueue(DateTimeOffset.UtcNow.ToString("O")+" "+value);while(journal.Count>200)journal.Dequeue();}
 internal void Export(){if(stateFile==null)return;string path=Path.Combine(Path.GetDirectoryName(stateFile)!,"hunt-diagnostics.txt");try{File.WriteAllLines(path,new[]{"Hunt Route "+typeof(Plugin).Assembly.GetName().Version}.Concat(journal));NoticeText="Диагностика сохранена: "+path;}catch(Exception e){NoticeText="Не удалось сохранить диагностику: "+e.Message;}}
 internal bool PersistenceBlocked=>settingsLoadPending;
 internal bool CompletionUncertain(Board board,bool weekly)=>(completionLoadPending||History.RequiresPickupEvidence)&&(weekly?new[]{board.Weekly}:board.Daily).Any(i=>BillStates.TryGetValue(i,out var s)&&s.CompletedCurrentSet&&!History.IsComplete(character,i,weekly,DateTimeOffset.UtcNow,s.AvailableId)&&!RepeatedSetAvailable(i,weekly,DateTimeOffset.UtcNow,s));
 internal bool BoardReachable(Board b)=>CurrentTerritory==b.Territory||IsAetheryteUnlocked(b.Aetheryte);
 internal string LocationLabel=>Session.Target is {IsEliteMark:true}?Search.Lost?"Последняя известная позиция":Search.ActorId!=null?"Элита обнаружена":"Предполагаемые места появления":ordinaryActor!=null&&Session.Target is {} goal&&ordinaryKind==JourneyFlow.Key(goal)?"Моб обнаружен":"Известная точка охоты";
 private bool RepeatedSetAvailable(byte bill,bool weekly,DateTimeOffset now,BillState state)=>
  !completionLoadPending&&!state.Taken&&state.CompletedCurrentSet
  &&History.IsPreviousCompletion(character,bill,weekly,now,state.AvailableId)
  &&!History.IsComplete(character,bill,weekly,now,state.AvailableId);
 internal bool CanPickup(Board b,bool weekly){
  var ids=weekly?new[]{b.Weekly}:b.Daily;
  var now=DateTimeOffset.UtcNow;
  return ids.Any(i=>!unreadBills.Contains(i)&&BillStates.TryGetValue(i,out var state)&&state.Unlocked&&state.AvailableId>0&&!state.Taken
   &&(!state.CompletedCurrentSet||RepeatedSetAvailable(i,weekly,now,state))&&!History.IsComplete(character,i,weekly,now,state.AvailableId));
 }
 private unsafe void ClearCompletedMarker(){
  var marker=ownedMarker;
  ownedMarker=null;
  ownedMarkerBill=null;
  if(marker is not {} expected||!expected.Matches(ReadFlag()))return;
  if(MarkerClearer is {} clear)clear();
  else if(AgentMap.Instance()!=null)AgentMap.Instance()->FlagMarkerCount=0;
 }
 private void CheckExpansionCompletionNotices(System.Action playSound){
  if(!Ready||completionLoadPending)return;
  List<Board>? completed=null;
  foreach(var board in Boards){
   bool reliable=board.Daily.Append(board.Weekly).All(i=>BillStates.ContainsKey(i)&&!unreadBills.Contains(i))
    &&!CompletionUncertain(board,false)&&!CompletionUncertain(board,true);
   if(!reliable)continue;
   var daily=CollectionProgress(board,false);
   var weekly=CollectionProgress(board,true);
   if(NativeDialogs!=null&&daily.Taken+weekly.Taken>0){
    var at=DateTimeOffset.UtcNow;
    var starts=CharacterSave.HuntStartedAt;
    long dailyReset=ResetClock.Period(at,false).ToUnixTimeSeconds();
    if(!starts.TryGetValue(board.Name,out long started)||started<dailyReset)
     starts[board.Name]=at.ToUnixTimeSeconds();
   }
   if(!expansionCompletionNotice.Observe(board.Name,daily,weekly,reliable))continue;
   (completed??=[]).Add(board);
  }
  if(completed==null)return;
  if(ownedMarkerBill is {} bill&&completed.Any(board=>board.Weekly==bill||board.Daily.Contains(bill)))
   ClearCompletedMarker();
  foreach(var board in completed)OfferHuntSummary(board);
  if(!Settings.ResetNotifications)return;
  const ushort completedGreen=45, availableYellow=31;
  var message=new SeStringBuilder().AddText("[Hunt Route] ");
  bool first=true;
  void AddStatus(Board board,string status,ushort color){
   if(!first)message.Add(Dalamud.Game.Text.SeStringHandling.Payloads.NewLinePayload.Payload);
   first=false;
   message.AddText(board.Name+" — ").AddUiForeground(status,color);
  }
  foreach(var board in completed)AddStatus(board,"выполнено",completedGreen);
  foreach(var board in Boards){
   if(completed.Contains(board))continue;
   bool daily=CanPickup(board,false),weekly=CanPickup(board,true);
   if(!daily&&!weekly)continue;
   string available=daily?(weekly?"обычные и элитный":"обычные"):"элитный";
   AddStatus(board,"можно взять: "+available,availableYellow);
  }
  Service.Chat.Print(message.Build());
  playSound();
 }
 private void OfferHuntSummary(Board board){
  if(NativeDialogs==null)return;
  var now=DateTimeOffset.UtcNow;
  long end=now.ToUnixTimeSeconds();
  bool timed=CharacterSave.HuntStartedAt.Remove(board.Name,out long started);
  var goals=CompletedGoals.Concat(Mobs.Where(Done))
   .Where(m=>(m.BillNumber==board.Weekly||board.Daily.Contains(m.BillNumber))&&
    History.IsComplete(character,m.BillNumber,m.BillNumber==board.Weekly,now,m.OrderId))
   .DistinctBy(m=>(m.BillNumber,m.OrderId,m.MobIndex)).ToArray();
  bool completeData=board.Daily.Append(board.Weekly).Where(b=>BillStates.GetValueOrDefault(b).Unlocked)
   .All(b=>goals.Any(m=>m.BillNumber==b));
  bool CanStart()=>Boards.Any(b=>b!=board&&BoardReachable(b)&&(CanPickup(b,false)||CanPickup(b,true)));
  bool next=CanStart();
  string currency=board.Name is "Heavensward" or "Stormblood"?"Печати Центурио":"Мешки орехов";
  timed=timed&&started>=ResetClock.Period(now,false).ToUnixTimeSeconds()&&started<=end;
  var elapsed=TimeSpan.FromSeconds(timed?end-started:0);
  string duration=timed?$"{(long)elapsed.TotalMinutes} мин {elapsed.Seconds} с":"Нет данных";
  var content=TravelDialogContent.Completion(board.Name,currency,
   completeData?goals.Sum(m=>(long)m.CurrencyReward):null,completeData?goals.Sum(m=>(long)m.GilReward):null,
   completeData?goals.Sum(m=>(long)m.NeededKills):null,duration,next);
  ulong owner=character;
  NativeDialogs.Offer(new("completion:"+board.Name,$"completion:{owner}:{board.Name}:{end}",content.Title,
   ()=>Ready&&character==owner,()=>!TravelBusy,
   ()=>{if(next&&CanStart()){PickupOpen=true;Open();}return true;},content));
 }
 private string? LoginMode()=>Mobs.Any(m=>AllTiers.Contains(m.BillNumber)&&!Done(m)&&!Flow.Excluded(m))?"taken":Boards.Any(b=>CanPickup(b,false)||CanPickup(b,true))?"pickup":null;
 private void OfferLoginNotice(){
  if(LoginMode() is not {} mode){loginNoticeShown=true;return;}
  if(Flow.Active&&Flow.Pauses==PauseReason.None){NoticeText="Предложение охоты ожидает паузы маршрута. Действия доступны в сводке.";return;}
  NativeDialogs.Offer(new("pickup",$"login:{character}:{mode}","Начать охоту?",
   ()=>Ready&&Settings.ResetNotifications&&LoginMode()==mode,
   ()=>!TravelBusy&&pendingJourney==null&&(!Flow.Active||Flow.Pauses!=PauseReason.None)&&!Flow.Pauses.HasFlag(PauseReason.Marker),
   ()=>{if(mode=="taken")ResumeJourney();else PickupOpen=mode=="pickup";Open();return true;},
   mode=="taken"?TravelDialogContent.ResumeHunt():TravelDialogContent.Pickup(),
   ()=>{
    loginNoticeShown=true;
    if(mode!="pickup")return;
    var now=DateTimeOffset.UtcNow;
    if(Boards.Any(b=>CanPickup(b,false)))CharacterSave.DailyNotice=ResetClock.Period(now,false).ToUnixTimeSeconds();
    if(Boards.Any(b=>CanPickup(b,true)))CharacterSave.WeeklyNotice=ResetClock.Period(now,true).ToUnixTimeSeconds();
    SaveJourney(true);
   }));
 }
 private void CheckResetNotice(){
  if(!Settings.ResetNotifications||!Ready||contractRevision<2)return;
  CheckResetChatNotice();
  if(!loginNoticeShown){OfferLoginNotice();return;}
  var now=DateTimeOffset.UtcNow;long daily=ResetClock.Period(now,false).ToUnixTimeSeconds(),weekly=ResetClock.Period(now,true).ToUnixTimeSeconds();
  bool d=CharacterSave.DailyNotice!=daily&&Boards.Any(b=>CanPickup(b,false));bool w=CharacterSave.WeeklyNotice!=weekly&&Boards.Any(b=>CanPickup(b,true));
  if(!d&&!w)return;
  if(Flow.Active&&Flow.Pauses==PauseReason.None){NoticeText="Доступны новые контракты — выберите доску в сводке.";return;}
  if(TravelBusy || pendingJourney!=null || Flow.Pauses.HasFlag(PauseReason.Marker))return;
  NativeDialogs.Offer(new("pickup",$"pickup:{character}:{(d?daily:0)}:{(w?weekly:0)}","Доступны новые контракты",()=>Ready&&Settings.ResetNotifications&&(d&&ResetClock.Period(DateTimeOffset.UtcNow,false).ToUnixTimeSeconds()==daily&&Boards.Any(b=>CanPickup(b,false))||w&&ResetClock.Period(DateTimeOffset.UtcNow,true).ToUnixTimeSeconds()==weekly&&Boards.Any(b=>CanPickup(b,true))),()=>!TravelBusy&&pendingJourney==null&&(!Flow.Active||Flow.Pauses!=PauseReason.None)&&!Flow.Pauses.HasFlag(PauseReason.Marker),()=>{PickupOpen=true;Open();return true;},TravelDialogContent.Pickup(),()=>{if(d&&ResetClock.Period(DateTimeOffset.UtcNow,false).ToUnixTimeSeconds()==daily&&Boards.Any(b=>CanPickup(b,false)))CharacterSave.DailyNotice=daily;if(w&&ResetClock.Period(DateTimeOffset.UtcNow,true).ToUnixTimeSeconds()==weekly&&Boards.Any(b=>CanPickup(b,true)))CharacterSave.WeeklyNotice=weekly;SaveJourney(true);}));
 }
 private void CheckResetChatNotice(){
  if(completionLoadPending||character==0||CurrentTerritory==0||pendingJourney!=null||TravelBusy||
     Service.Condition.Any(ConditionFlag.BoundByDuty,ConditionFlag.BoundByDuty56,ConditionFlag.BoundByDuty95,
       ConditionFlag.InDeepDungeon,ConditionFlag.ChocoboRacing,ConditionFlag.DutyRecorderPlayback))return;
  var now=DateTimeOffset.UtcNow;
  long daily=ResetClock.Period(now,false).ToUnixTimeSeconds(),weekly=ResetClock.Period(now,true).ToUnixTimeSeconds();
  var saved=CharacterSave;
  bool checkDaily=saved.DailyChatNotice!=daily,checkWeekly=saved.WeeklyChatNotice!=weekly;
  if(!checkDaily&&!checkWeekly)return;
  if(!Service.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(CurrentTerritory,out var territory)||
     territory.ContentFinderCondition.RowId!=0)return;
  bool dailyAvailable=false,weeklyAvailable=false;
  var message=new SeStringBuilder().AddText("[Hunt Route] Сброс охоты. Доступны контракты:");
  foreach(var board in Boards){
   bool d=checkDaily&&CanPickup(board,false),w=checkWeekly&&CanPickup(board,true);
   if(!d&&!w)continue;
   dailyAvailable|=d;weeklyAvailable|=w;
   message.Add(Dalamud.Game.Text.SeStringHandling.Payloads.NewLinePayload.Payload);
   message.AddText(board.Name+" — ").AddUiForeground(d?(w?"ежедневные и недельный элитный":"ежедневные"):"недельный элитный",31);
  }
  if(!dailyAvailable&&!weeklyAvailable)return;
  Service.Chat.Print(message.Build());
  if(dailyAvailable)saved.DailyChatNotice=daily;
  if(weeklyAvailable)saved.WeeklyChatNotice=weekly;
  SaveJourney(true);
 }
}
