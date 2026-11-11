using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace HuntRoute;

public record Board(string Name, byte[] Daily, byte Weekly, uint Aetheryte, uint Territory, uint Map, Vector2 Position);
public record EntryPoint(uint Id, string Name, Vector2 Position);
public record ZoneRoute(string Name, List<MobHuntEntry> Mobs, EntryPoint? Start, bool StartsHere=false,float EstimatedCost=0,uint GilCost=uint.MaxValue,uint Region=0);

public sealed partial class Plugin : IDalamudPlugin {
    public static readonly Board[] Boards = [
        new("Heavensward", [1,2,3], 5, 70, 418, 218, new(12.718168f,11.681350f)),
        new("Stormblood", [6,7,8], 9, 111, 628, 370, new(10.609598f,10.362924f)),
        new("Shadowbringers", [10,11,12], 13, 133, 819, 497, new(9.568138f,9.425624f)),
        new("Endwalker", [14,15,16], 17, 182, 962, 693, new(11.834738f,13.205691f)),
        new("Dawntrail", [18,19,20], 21, 216, 1185, 855, new(13.891332f,13.685839f))
    ];
    internal List<MobHuntEntry> Mobs = [];
    internal List<ZoneRoute> Routes = [];
    internal string? Error;
    internal bool Ready;
    internal Dictionary<(byte Bill, byte Mob), int> KillCounts = new();
    internal Dictionary<byte,BillState> BillStates = new();
    private readonly WindowSystem windows = new("HuntRoute");
    private readonly HuntWindow window;
    private MiniWindow? miniWindow;
    internal readonly EliteWindow Elite;
    internal readonly NextZoneWindow NextZone;
    internal readonly ShortcutWindow Shortcut;
    internal readonly NativeDialogService NativeDialogs;
    internal NativeHuntUi? NativeMain;
    private CompletionHistory? completionHistory;
    private List<(ulong Character,byte Bill,bool Weekly,DateTimeOffset At,BillState State)>? pendingCompletionObservations;
    private Dictionary<(ulong Character,byte Bill),(long Period,BillState State)>? pendingCompletionSnapshots;
    private CompletionHistory History => completionHistory ??= new();
    private string? completionFile;
    private long nextHistorySave;
    private bool completionLoadPending;
    private bool wasLoading;
    private ShortcutTiming? shortcutTiming;
    private ShortcutTiming Timing => shortcutTiming ??= new();
    private long nextScan,nextEliteScan;
    private HashSet<byte>? unread;
    private HashSet<byte> unreadBills=>unread??=new();
    private BillSnapshotGuard? billGuard;
    private BillSnapshotGuard Guard=>billGuard??=new();
    private HashSet<byte>? uncertain;
    private HashSet<byte> uncertainBills=>uncertain??=new();
    private string signature = "";
    private ulong character;
    private string routeSignature="";
    private readonly Dictionary<uint,IReadOnlyDictionary<uint,uint>> observedFares=[];
    private long nextTeleportAllowed;
    internal uint CurrentTerritory;
    internal uint CurrentMap;
    internal bool HideDone = true;
    internal readonly RouteSession Session = new();
    public Plugin(IDalamudPluginInterface pi) {
        pi.Create<Service>();
        completionFile=System.IO.Path.Combine(pi.GetPluginConfigDirectory(),"completion-history.json");
        try { completionHistory=CompletionHistory.LoadRecovering(completionFile); }
        catch(Exception e) { completionLoadPending=true;completionHistory=new(){RequiresPickupEvidence=true};Service.PluginLog.Error(e,"Не удалось загрузить историю выполнения охоты"); }
        if(History.Recovered)NoticeText="История выполнения восстановлена из резервной копии. Неподтверждённые отметки не выдаются за выполненные.";
        InitializeStore();
        NativeDialogs=new(pi);
        NativeMain=new(this,NativeDialogs);
        window = new(this); windows.AddWindow(window);
        Elite = new(this); windows.AddWindow(Elite);
        miniWindow=new(this);windows.AddWindow(miniWindow);
        NextZone=new(this);
        Shortcut=new(this);
        Service.Commands.AddHandler("/huntroute", new CommandInfo((_, args) => { switch(args.Trim().ToLowerInvariant()){case "pause":TogglePause();break;case "resume":ResumeJourney();break;case "stop":StopJourney();break;case "mini":SetMini(!Settings.Mini);break;case "classic":OpenClassic();break;case "settings":OpenConfig();break;default:NativeMain.Toggle();break;} }) { HelpMessage = "Hunt Route: нативное меню охоты; classic — резервное окно мыши" });
        pi.UiBuilder.Draw += windows.Draw; pi.UiBuilder.Draw += NativeDialogs.DrawFallback; pi.UiBuilder.Draw += Elite.DrawWorld; pi.UiBuilder.OpenMainUi += Open; pi.UiBuilder.OpenConfigUi += OpenConfig;
        Service.Framework.Update += Update;
        Service.ClientState.Logout += OnLogout;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
    }
    private void Open(){if(NativeMain!=null)NativeMain.Open();else if(window!=null)window.IsOpen=true;}
    private void OpenConfig(){if(NativeMain!=null)NativeMain.Open(NativeHuntPage.Settings);else{SettingsOpen=true;Open();}}
    internal void OpenClassic(){NativeMain?.CloseMain();if(window!=null)window.IsOpen=true;}
    internal void HideClassic(){if(window!=null)window.IsOpen=false;}
    internal void Refresh() { nextScan = 0; signature = ""; }
    private void OnLogout(int type,int code) {
        NativeMain?.CloseAll();
        SaveJourney(true);
        SaveHistory(true);
        Ready=false; observedFares.Clear(); character=0; signature=""; routeSignature=""; nextScan=0; nextTeleportAllowed=0;
        wasLoading=false; Timing.Reset(); CurrentTerritory=0; CurrentMap=0; Mobs.Clear(); Routes.Clear(); KillCounts.Clear(); BillStates.Clear();
        Elite.Selected=null; Session.Stop(); NativeDialogs.CancelAll(); NextZone.Sync(); Shortcut.Sync();
    }
    private void OnTerritoryChanged(uint territory) {
        nextScan=0; routeSignature=""; Session.ZoneTransition(); Search.Suspend();externalTransition=true;
    }
    private unsafe void Update(IFramework _) {
        // Loading can temporarily clear LocalPlayer/ContentId. Only the logout event clears the route.
        bool loading=Service.Condition.Any(ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51);
        if(loading) { if(pendingJourney!=null)pendingJourney.Loaded=true; Timing.Loading(Now);if(!wasLoading) { Session.ZoneTransition();Search.Suspend(); } wasLoading=true; return; }
        if(wasLoading) { wasLoading=false;nextScan=0;routeSignature="";Session.ZoneTransition(); }
        if(!Service.ClientState.IsLoggedIn || !Service.Player.IsLoaded || Service.Player.ContentId==0) {
            Ready=false; Search.Suspend();return;
        }
        try {
            NativeMain?.Pump();
            long now=Now;
            if(now<nextScan) {
                // Contract snapshots stay on the 1-second cadence; fly-by detection runs at 10 Hz.
                if(Ready && character==Service.Player.ContentId && now>=nextEliteScan && Session.Phase==RoutePhase.Following
                    && !Service.Condition.Any(ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent) && now>=nextTeleportAllowed) {
                    nextEliteScan=now+100;CheckMarker();DetectorNavigation();UpdateEliteSearch();
                }
                return;
            }
            nextScan=now+1000;

            if (character != Service.Player.ContentId) { if(character!=0)SaveJourney(true);NativeDialogs.CancelAll();character = Service.Player.ContentId; observedFares.Clear(); signature = ""; routeSignature=""; nextTeleportAllowed=0; Elite.Selected=null; Session.Stop(); LoadJourney(); NextZone.Sync(); Shortcut.Sync(); }
            if(!RecoverJourneyStore()){Ready=false;Error=JourneySaveError;return;}
            var hunt = MobHunt.Instance(); if (hunt == null) { Ready=false; return; }
            for(byte bill=0;bill<22;bill++){
                var kills=new int[5];for(byte mob=0;mob<5;mob++)kills[mob]=hunt->GetKillCount(bill,mob);
                AcceptBillSnapshot(bill,new((hunt->ObtainedFlags&(1<<bill))!=0,hunt->ObtainedMarkId[bill],kills),Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(bill).OrderAmount);
            }
            string current = string.Join(",",Enumerable.Range(0,22).Select(i=>$"{Guard.Current((byte)i)?.Taken}:{Guard.Current((byte)i)?.Order}")) + ":" + string.Join(",", Service.Aetherytes.Select(a=>a.AetheryteId).Order());
            if (current != signature || unreadBills.Count>0) {
                ReadBills(); signature = current; routeSignature="";
                if(Elite.Selected is {} old) Elite.Selected=Mobs.FirstOrDefault(m=>m.BillNumber==old.BillNumber && m.MobIndex==old.MobIndex && m.MobHuntId==old.MobHuntId);
            }
            CurrentTerritory=Service.ClientState.TerritoryType; CurrentMap=Service.ClientState.MapId;
            var fares=Service.Aetherytes.GroupBy(a=>a.AetheryteId).ToDictionary(g=>g.Key,g=>g.Min(a=>a.GilCost));
            if(fares.Count>0)observedFares[CurrentTerritory]=fares;
            string routeKey=current+":"+string.Join(",",fares.OrderBy(p=>p.Key).Select(p=>$"{p.Key}={p.Value}"))+":"+CurrentTerritory+":"+CurrentMap+":"+string.Join(",",Mobs.Where(Done).Select(m=>$"{m.BillNumber}-{m.MobIndex}"));
            if(routeSignature!=routeKey) { BuildRoutes(fares); routeSignature=routeKey; }
            var snapshots=new Dictionary<byte,BillState>();
            for(byte bill=0;bill<22;bill++) {
                if(unreadBills.Contains(bill)){if(BillStates.TryGetValue(bill,out var known))snapshots[bill]=known;continue;}
                var accepted=Guard.Current(bill);if(accepted==null)continue;
                bool obtained=accepted.Taken;
                var type=Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(bill);
                byte retainedId=accepted.Order;
                bool complete=false;
                // Completed bills clear ObtainedFlags, but the game retains their order ID and kills.
                if(retainedId>0 && retainedId<=type.OrderAmount && Service.DataManager.GetSubrowExcelSheet<MobHuntOrder>().TryGetRow(type.OrderStart.RowId+retainedId-1u,out var rows)) {
                    var requirements=rows.Where(o=>o.Target.RowId!=0).Select(o=>new BillRequirement((byte)o.SubrowId,o.NeededKills));
                    complete=BillCompletion.IsComplete(retainedId,type.OrderAmount,requirements,m=>KillCounts.GetValueOrDefault((bill,m)));
                }
                snapshots[bill]=BillState.FromGame(hunt->IsMarkBillUnlocked(bill),obtained,retainedId,hunt->AvailableMarkId[bill],complete,type.OrderAmount);
            }
            var observedAt=DateTimeOffset.UtcNow;
            foreach(var pair in snapshots.Where(p=>!unreadBills.Contains(p.Key))) ObserveCompletion(character,pair.Key,Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(pair.Key).Type==2,observedAt,pair.Value);
            SaveHistory();
            BillStates=snapshots;
            contractAt=Now;contractRevision++;
            Ready = true; Error = null;
            ReconcileContext();ReconcileTravel();if(pendingJourney==null)Timing.Ready(contractAt);CheckMarker();AdvanceRoute();CheckResetNotice();NativeDialogs.Pump();AuditState();SaveJourney();
        } catch (Exception e) { Ready = false; Error = e.Message; nextScan=Now+10000; Service.PluginLog.Error(e, "Reading hunt bills"); }
    }
    private unsafe void ReadBills() {
        var previous=Mobs;var result=new List<MobHuntEntry>();var hunt=MobHunt.Instance();if(hunt==null)return;
        unreadBills.Clear();var orders=Service.DataManager.GetSubrowExcelSheet<MobHuntOrder>();
        for(byte bill=0;bill<22;bill++) {
            try {
                if(uncertainBills.Contains(bill)){unreadBills.Add(bill);result.AddRange(previous.Where(m=>m.BillNumber==bill));continue;}
                var accepted=Guard.Current(bill);if(accepted is not {Taken:true,Order:>0})continue;
                var type=Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(bill);
                byte orderId=accepted.Order;
                if(orderId>type.OrderAmount || !orders.TryGetRow(type.OrderStart.RowId+orderId-1u,out var rows))throw new InvalidOperationException("Контракт ещё не доступен в данных игры");
                var batch=new List<MobHuntEntry>();
                foreach(var order in rows) {
                    if(order.Target.RowId==0||order.NeededKills==0||order.SubrowId>=5)continue;
                    var target=order.Target.Value;var map=target.Map.Value;
                    batch.Add(new(){Generation=CharacterSave.Contracts.GetValueOrDefault(bill)?.Generation??0,OrderId=orderId,Name=target.Name.Value.Singular.ToString(),TerritoryName=map.PlaceName.Value.Name.ToString(),ExpansionName=map.TerritoryType.Value.ExVersion.Value.Name.ToString(),ExpansionId=map.TerritoryType.Value.ExVersion.RowId,TerritoryType=map.TerritoryType.RowId,MapId=map.RowId,MobHuntId=target.Name.RowId,IsEliteMark=type.Type==2,BillNumber=bill,MobIndex=(byte)order.SubrowId,NeededKills=order.NeededKills,Icon=target.Icon});
                }
                if(batch.Count==0)throw new InvalidOperationException("Цели контракта ещё не загружены");
                result.AddRange(batch);
            } catch(Exception e) {
                unreadBills.Add(bill);result.AddRange(previous.Where(m=>m.BillNumber==bill));
                Flow.Status=$"Данные контракта {bill} недоступны: {e.Message}. Остальные цели продолжают работать.";NoticeText=Flow.Status;foreach(var old in previous.Where(m=>m.BillNumber==bill))Flow.Unavailable[JourneyFlow.Key(old)]="Данные контракта временно недоступны";
                Service.PluginLog.Debug(e,"Временная недоступность контракта {Bill}",bill);
            }
        }
        Mobs=result;
    }
    internal List<EntryPoint> Entries(uint mapId, uint territory) {
        var map = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(mapId);
        if(map.TerritoryType.RowId!=territory) return [];
        var unlocked = Service.Aetherytes.Select(a => a.AetheryteId).ToHashSet();
        var result = new List<EntryPoint>();
        if(!Service.DataManager.GetSubrowExcelSheet<MapMarker>().TryGetRow(map.MapMarkerRange,out var markers)) return result;
        foreach (var marker in markers) {
            if (marker.DataType != 3 || !unlocked.Contains(marker.DataKey.RowId)) continue;
            if(!Service.DataManager.GetExcelSheet<Aetheryte>().TryGetRow(marker.DataKey.RowId,out var a)) continue;
            if (a.IsAetheryte && a.Territory.RowId == territory) result.Add(new(a.RowId, a.PlaceName.Value.Name.ToString(), MapCoordinates.FromTexturePixel(new Vector2(marker.X,marker.Y),map)));
        }
        // Hinterlands can only be entered via Idyllshire; do not fabricate an in-zone distance.
        return result;
    }
    private void BuildRoutes(IReadOnlyDictionary<uint,uint> fares) {
        var result=new List<ZoneRoute>();
        foreach(var group in Mobs.GroupBy(m=>(m.TerritoryType,m.MapId)).OrderBy(g=>g.Key.TerritoryType)) {
            try {
                var targets=group.OrderBy(m=>m.MobHuntId).ThenBy(m=>m.BillNumber).ToList();
                var entries=Entries(group.Key.MapId,group.Key.TerritoryType);
                bool here=CurrentTerritory==group.Key.TerritoryType;
                var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(group.Key.MapId);
                Vector2[] origins=entries.Select(e=>MapCoordinates.ToWorld(e.Position,map)).ToArray();
                if(here&&CurrentMap==group.Key.MapId&&Service.ObjectTable.LocalPlayer is {} player)origins=[new(player.Position.X,player.Position.Z)];
                bool reachable=here||entries.Count>0||group.Key.TerritoryType==399&&Service.Aetherytes.Any(a=>a.AetheryteId==75);
                var(order,start,cost)=RoutePlanner.Zone(targets,m=>!reachable||Done(m)||Flow.Excluded(m),m=>m.IsEliteMark,PlanningPoints,origins,here?null:entries.Select(e=>fares.GetValueOrDefault(e.Id,uint.MaxValue)).ToArray(),here?0:RoutePlanner.GilPerSecond);
                EntryPoint? entry=!here&&start>=0?entries[start]:null;
                if(!here&&entry==null&&order.FirstOrDefault(m=>!Done(m)&&!Flow.Excluded(m)) is {} first)entry=NearestEntryFor(first);
                result.Add(new(group.First().TerritoryName??"Зона",order.ToList(),entry,here,cost,here?0:entry!=null?fares.GetValueOrDefault(entry.Id,uint.MaxValue):uint.MaxValue,map.PlaceNameRegion.RowId));
            }catch(Exception e){foreach(var m in group)Flow.Unavailable[JourneyFlow.Key(m)]="Ошибка связи карты и координат: "+e.Message;result.Add(new(group.First().TerritoryName??"Зона",group.ToList(),null));Service.PluginLog.Debug(e,"Проверка зоны {Territory}",group.Key.TerritoryType);}
        }
        var remote=result.Where(r=>!r.StartsHere&&r.Start!=null&&r.GilCost!=uint.MaxValue&&r.Mobs.Any(m=>!Done(m)&&!Flow.Excluded(m)&&PlanningPoints(m).Length>0)).ToArray();
        uint region=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().TryGetRow(CurrentMap,out var currentMap)?currentMap.PlaceNameRegion.RowId:0;
        var stops=remote.Select(r=>new RoutePlanner.Stop(r.Mobs[0].TerritoryType,r.Region,r.Start!.Id,r.GilCost,r.EstimatedCost)).ToArray();
        var orderByTrip=RoutePlanner.PlanJourney(stops,CurrentTerritory,region,Timing.OverheadSeconds,
            (from,to)=>observedFares.TryGetValue(from,out var prices)&&prices.TryGetValue(to,out uint price)?price:null);
        var planned=orderByTrip.Select(i=>remote[i]).ToArray();
        Routes=result.Where(r=>r.StartsHere).Concat(planned).Concat(result.Where(r=>!r.StartsHere&&!planned.Contains(r))).ToList();
    }
    internal int Kills(MobHuntEntry m) => Math.Clamp(KillCounts.GetValueOrDefault((m.BillNumber,m.MobIndex)),0,(int)Math.Min(m.NeededKills,(uint)int.MaxValue));
    internal bool Done(MobHuntEntry m) => Kills(m) >= m.NeededKills;
    internal bool TravelBusy => Service.Condition.Any(ConditionFlag.InCombat,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,
            ConditionFlag.Unconscious,ConditionFlag.Occupied,ConditionFlag.OccupiedInEvent,ConditionFlag.OccupiedInQuestEvent,
            ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.WatchingCutscene,ConditionFlag.Casting)
            || Service.ObjectTable.LocalPlayer?.IsCasting==true || Now<nextTeleportAllowed;
    internal unsafe bool Teleport(uint id,bool allowSameZone=false) {
        if(!Service.DataManager.GetExcelSheet<Aetheryte>().TryGetRow(id,out var destination) || !destination.IsAetheryte) {
            Service.Chat.PrintError("Неизвестный эфирит."); return false;
        }
        var decision=TravelPolicy.Decide(Service.ClientState.TerritoryType,destination.Territory.RowId,
            Ready && Service.ClientState.IsLoggedIn,Service.Aetherytes.Any(a=>a.AetheryteId==id),TravelBusy,allowSameZone);
        if(decision==TravelDecision.AlreadyThere) return true;
        if(decision!=TravelDecision.Teleport) {
            Service.Chat.PrintError(decision==TravelDecision.Locked?"Эфирит не открыт.":decision==TravelDecision.NotReady?"Данные персонажа ещё не готовы.":"Телепорт сейчас недоступен или уже запрошен.");
            return false;
        }
        var telepo=TeleportTransport==null?Telepo.Instance():null;
        if(TeleportTransport!=null ? !TeleportTransport(id) : telepo==null || telepo->ActiveTeleportRequest || !telepo->Teleport(id,0)) { Service.Chat.PrintError("Игра отклонила телепорт или уже выполняет его."); return false; }
        Timing.Begin(Now);
        nextTeleportAllowed=Now+3000;
        return true;
    }
    internal readonly EliteSearch Search = new();
    private string searchKey="";
    internal bool HasPosition(MobHuntEntry m) {
        if(Flow.Excluded(m))return false;
        bool known=livePositions.ContainsKey(JourneyFlow.Key(m)) || m.IsEliteMark&&Session.Target is {} lockedTarget&&JourneyFlow.Key(lockedTarget)==JourneyFlow.Key(m)&&Search.LockedPosition!=null || (m.IsEliteMark?EliteSpawns.For(m).Length>0:Location.Database.ContainsKey(m.MobHuntId));
        if(!known){Flow.Unavailable[JourneyFlow.Key(m)]="Нет проверенных координат";return false;}
        Flow.Unavailable.Remove(JourneyFlow.Key(m));return true;
    }
    internal void Travel(MobHuntEntry m) {
        if(!Ready || !Service.ClientState.IsLoggedIn || unreadBills.Contains(m.BillNumber) || !HasPosition(m)) return;
        if(Done(m)) { Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name); return; }
        EntryPoint? requestedEntry=null;
        if(Service.ClientState.TerritoryType!=m.TerritoryType) {
            var entry=EntryFor(m);requestedEntry=entry;
            if(entry!=null) { if(!Teleport(entry.Id)) return; }
            else if(m.TerritoryType==399) { if(!Teleport(75)) return; }
            else { Service.Chat.PrintError("Нет открытого эфирита на этой карте."); return; }
        }
        // Re-selecting a locked elite must not restart its spawn search, including after a teleport.
        if(m.IsEliteMark && Search.LockedPosition!=null && Session.Target is {} tracked && tracked.BillNumber==m.BillNumber && tracked.MobIndex==m.MobIndex && tracked.MobHuntId==m.MobHuntId && searchKey==SearchIdentity(m)) {
            Search.Suspend();Session.ZoneTransition();AdvanceRoute();return;
        }
        var board=Boards.FirstOrDefault(b=>b.Daily.Contains(m.BillNumber) || b.Weekly==m.BillNumber);
        if(board!=null) {
            Flow.Active=true;
            if(Session.Target is {} prior&&JourneyFlow.Key(prior)!=JourneyFlow.Key(m))ResetTargetTracking();
            if(Session.Phase==RoutePhase.Idle || Session.Phase==RoutePhase.Finished)Session.Start(m,AllTiers);else Session.Continue(m);
            if(Service.ClientState.TerritoryType!=m.TerritoryType&&requestedEntry!=null){
                var destination=Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(requestedEntry.Id).Territory.Value;
                pendingJourney=new(Service.ClientState.TerritoryType,destination.RowId,destination.Map.RowId,Now,JourneyFlow.Key(m),false);
            }
            AdvanceRoute();SaveJourney(true);
        }
        else Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name);
    }
    internal void AdvanceRoute() {
        if(Ready&&Session.Target is {IsEliteMark:true} credited&&!unreadBills.Contains(credited.BillNumber)&&Done(credited)){Search.Start([]);searchKey="";if(Flow.Manual==JourneyFlow.Key(credited))Flow.Manual=null;}
        if(!Ready || Service.Condition.Any(ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.Casting,ConditionFlag.Unconscious) || Now<nextTeleportAllowed) { Search.Suspend();return; }
        if(pendingJourney!=null){Search.Suspend();return;}
        if(Session.Target is {} uncertainTarget&&unreadBills.Contains(uncertainTarget.BillNumber)){Search.Suspend();Flow.Status="Данные текущего контракта уточняются. Цель сохранена.";NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");return;}
        if(Flow.Active&&Session.Phase==RoutePhase.AwaitingTravel&&Session.Target is {} planned&&Done(planned)){
            NativeDialogs.Cancel("travel");ResetTargetTracking();var remaining=BestTarget();
            if(remaining==null){Session.Stop();Flow.Active=false;Flow.Status="Доступные цели выполнены. Проверьте отложенные.";SaveJourney(true);return;}
            if(remaining.TerritoryType==Service.ClientState.TerritoryType)Session.Continue(remaining);else Session.PlanTravel(remaining,AllTiers);
        }
        var manual=ManualTarget();
        if(Flow.Active&&manual!=null&&Flow.Pauses.HasFlag(PauseReason.Unavailable))RefreshLive();
        if(Flow.Active&&manual!=null&&!Eligible(manual)){
            PauseUnavailableManual();return;
        }
        if(Flow.Active && Flow.Pauses.HasFlag(PauseReason.Unavailable) && (manual!=null||Routes.SelectMany(r=>r.Mobs).Any(Eligible))){
            Flow.Pauses&=~PauseReason.Unavailable;
            CheckMarker();
        }
        if(Flow.Active && Flow.Manual==null && Flow.Pauses.HasFlag(PauseReason.NoZone)){
            var local=Routes.SelectMany(r=>r.Mobs).FirstOrDefault(m=>m.TerritoryType==Service.ClientState.TerritoryType&&Eligible(m));
            if(local!=null){Flow.Pauses&=~PauseReason.NoZone;Session.Continue(local);}
        }
        if(Flow.Manual!=null && !Mobs.Any(m=>JourneyFlow.Key(m)==Flow.Manual&&!Done(m)))Flow.Manual=null;
        if(Flow.Active && Session.Target is {} excluded && !Eligible(excluded) && !Done(excluded) && Session.Phase!=RoutePhase.NextZone){
            var available=Routes.SelectMany(r=>r.Mobs).FirstOrDefault(Eligible);
            if(available!=null){if(available.TerritoryType==Service.ClientState.TerritoryType)Session.Retarget(available);else Session.RequestNext(available);}else{Flow.Pauses|=PauseReason.Unavailable;Flow.Status="Доступных целей нет. Проверьте отложенные.";}
        }
        if(Flow.Active && Flow.Pauses!=PauseReason.None){
            Search.Suspend();
            if(Session.Target is {} paused && Done(paused)){Search.Start([]);searchKey="";}
            else UpdateEliteSearch();
            NativeDialogs.Cancel("next");NativeDialogs.Cancel("shortcut");return;
        }
        if(Flow.Active && Session.Phase==RoutePhase.NextZone){
            var local=Routes.SelectMany(r=>r.Mobs).FirstOrDefault(m=>m.TerritoryType==Service.ClientState.TerritoryType&&Eligible(m));
            if(local!=null)Session.Retarget(local);
        }
        var mark=Flow.Active && Session.Phase==RoutePhase.NextZone ? null : Session.Tick(Routes.SelectMany(r=>r.Mobs).ToArray(),Done,m=>!Flow.Active?HasPosition(m):Eligible(m),Service.ClientState.TerritoryType);
        if(Session.Phase==RoutePhase.NextZone && Flow.Active)Session.Suggest(Routes.SelectMany(r=>r.Mobs).FirstOrDefault(m=>m.TerritoryType!=Service.ClientState.TerritoryType&&Eligible(m)));
        bool detected=DetectorNavigation();
        if(!detected && mark!=null && !mark.IsEliteMark && Session.Target==mark && Location.Database.TryGetValue(mark.MobHuntId,out var p)) {
            var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(mark.MapId);
            if(!PlaceFlag(mark.TerritoryType,mark.MapId,MapCoordinates.ToWorld(p.Coordinate,map)))Session.RetryMarker();
        }
        UpdateEliteSearch();NextZone.Sync();Shortcut.Sync();
        if(Flow.Active && Session.Phase==RoutePhase.Finished){Flow.Active=false;Flow.Status="Доступный маршрут завершён. Отложенные цели показаны отдельно.";SaveJourney(true);}
    }
    internal IEnumerable<Dalamud.Game.ClientState.Objects.Types.IBattleNpc> DetectedElites() {
        var ids=Mobs.Where(m=>AllTiers.Contains(m.BillNumber) && m.IsEliteMark && m.TerritoryType==Service.ClientState.TerritoryType && !Done(m) && !Flow.Excluded(m) && !unreadBills.Contains(m.BillNumber)).Select(m=>m.MobHuntId).ToHashSet();
        return Service.ObjectTable.OfType<Dalamud.Game.ClientState.Objects.Types.IBattleNpc>().Where(n=>!n.IsDead && ids.Contains(n.NameId));
    }
    private void UpdateEliteSearch() {
        if(Session.Target is not {IsEliteMark:true} target || Session.Phase is RoutePhase.Idle or RoutePhase.Finished or RoutePhase.Changed or RoutePhase.NextZone) {
            if(!string.IsNullOrEmpty(searchKey)) { searchKey="";Search.Start([]); }return;
        }
        if(Session.Phase!=RoutePhase.Following || Service.ClientState.MapId!=target.MapId || Service.ObjectTable.LocalPlayer is not {} player) { Search.Suspend();return; }
        var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(target.MapId);
        string key=SearchIdentity(target);
        bool firstPoint=key!=searchKey;
        if(firstPoint) {
            Search.Start(EliteSpawns.For(target).Select(p=>MapCoordinates.ToWorld(p,map)).ToArray());searchKey=key;
            if(restoreSearch is {} saved){Search.Restore(saved);restoreSearch=null;}
        }
        if(pendingJourney!=null || Service.Condition.Any(ConditionFlag.Unconscious,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent)){Search.Suspend();return;}
        var actors=Service.ObjectTable.OfType<Dalamud.Game.ClientState.Objects.Types.IBattleNpc>().Where(n=>n.NameId==target.MobHuntId)
            .Select(n=>new EliteActor(n.GameObjectId,new Vector2(n.Position.X,n.Position.Z),n.IsDead)).ToArray();
        ulong? previous=Search.ActorId;
        var point=Search.Observe(new Vector2(player.Position.X,player.Position.Z),actors,Service.Condition[ConditionFlag.InCombat] || player.IsCasting,Now,Done(target),contractRevision,contractAt,!unreadBills.Contains(target.BillNumber),NavigationAllowed);
        if(previous!=Search.ActorId && Search.ActorId!=null && Flow.Active)Ping("Элита обнаружена: "+target.Name,Settings.EliteSound);
        if(Search.Round>=4 && Flow.LongNotified.Add(JourneyFlow.Key(target)))Ping("Три круга без цели: "+target.Name+". Поиск продолжается.");
        if(point is {} position && !PlaceFlag(target.TerritoryType,target.MapId,position))Search.Retry();
    }

    private unsafe bool FlightKnown()=>FlightCapability?.Invoke() ?? (Service.Condition[ConditionFlag.InFlight] || FFXIVClientStructs.FFXIV.Client.Game.Control.Control.CanFly);
    internal ShortcutOffer? FindShortcut() {
        if(!Ready||Session.Phase!=RoutePhase.Following)return null;
        if(Flow.Active && !FlightKnown())return null;
        if(!Settings.Shortcuts || !NavigationAllowed || !Ready || Session.Phase!=RoutePhase.Following || Session.Target is not {} target
            || Service.ClientState.TerritoryType!=target.TerritoryType || Service.ClientState.MapId!=target.MapId
            || Service.ObjectTable.LocalPlayer is not {} player || Done(target)) return null;
        var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(target.MapId);
        string key=JourneyFlow.Key(target);
        Vector2 destination;
        if(!target.IsEliteMark&&ordinaryActor!=null&&ordinaryKind==key&&ordinaryPosition is {} tracked)destination=tracked;
        else if(livePositions.TryGetValue(key,out var live))destination=live;
        else if(Location.Database.TryGetValue(target.MobHuntId,out var position))destination=MapCoordinates.ToWorld(position.Coordinate,map);
        else return null;
        var here=new Vector2(player.Position.X,player.Position.Z);

        var prices=Service.Aetherytes.GroupBy(a=>a.AetheryteId).ToDictionary(g=>g.Key,g=>g.Min(a=>a.GilCost));
        var candidates=Entries(target.MapId,target.TerritoryType).Where(e=>prices.ContainsKey(e.Id))
            .Select(e=>new ShortcutCandidate(e.Id,e.Name,MapCoordinates.ToWorld(e.Position,map),prices[e.Id]));
        return ShortcutAdvisor.Evaluate(Session.RunId,target,here,destination,candidates,Timing.OverheadSeconds,Settings.MaxGil,Settings.MinSeconds,Settings.MinPercent/100f);
    }
    internal bool ShortcutStillValid(ShortcutOffer offered) =>
        FindShortcut() is {} current && ShortcutAdvisor.MatchesQuote(offered,current);
    internal bool AcceptShortcut(ShortcutOffer offered) {
        if(!ShortcutStillValid(offered) || TravelBusy) {
            Service.Chat.PrintError("Предложение изменилось или больше не сокращает путь. Проверьте обновлённые данные."); return false;
        }
        var destinationEntry=Entries(Session.Target!.MapId,offered.Territory).FirstOrDefault(e=>e.Id==offered.Aetheryte);
        if(destinationEntry==null||Service.ObjectTable.LocalPlayer is not {} sourcePlayer)return false;
        var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(Session.Target.MapId);
        var origin=new Vector2(sourcePlayer.Position.X,sourcePlayer.Position.Z);
        var originContext=ContextKey();
        var arrival=MapCoordinates.ToWorld(destinationEntry.Position,map);
        if(!Teleport(offered.Aetheryte,true))return false;
        pendingJourney=new(CurrentTerritory,offered.Territory,Session.Target.MapId,Now,JourneyFlow.Key(Session.Target),false){ArrivalPoint=arrival,Origin=origin,OriginContext=originContext};
        Session.ZoneTransition();nextScan=0;routeSignature="";
        return true;
    }
    internal ContractProgress CollectionProgress(Board b,bool weekly) {
        if(!Ready) return default;
        var now=DateTimeOffset.UtcNow;var indices=weekly?new[]{b.Weekly}:b.Daily;
        var states=indices.ToDictionary(i=>i,i=> {
            var state=BillStates.GetValueOrDefault(i);
            return state with {PeriodCompleted=History.IsComplete(character,i,weekly,now,state.AvailableId),RepeatedSetAvailable=RepeatedSetAvailable(i,weekly,now,state)};
        });
        return ContractProgress.Summarize(indices,states);
    }
    private void ObserveCompletion(ulong id,byte bill,bool weekly,DateTimeOffset at,BillState state) {
        if(completionLoadPending&&id!=0&&state.Unlocked){
            var snapshot=(ResetClock.Period(at,weekly).ToUnixTimeSeconds(),state);
            var snapshots=pendingCompletionSnapshots??=new();
            if(!snapshots.TryGetValue((id,bill),out var previous)||previous!=snapshot){
                snapshots[(id,bill)]=snapshot;
                (pendingCompletionObservations??=new()).Add((id,bill,weekly,at,state));
            }
        }
        History.Observe(id,bill,weekly,at,state);
    }
    private void SaveHistory(bool force=false) {
        if(completionLoadPending && completionFile!=null && (force || Now>=nextHistorySave)){
            try{
                var recovered=CompletionHistory.LoadRecovering(completionFile);
                if(pendingCompletionObservations!=null)
                    foreach(var observation in pendingCompletionObservations)
                        recovered.Observe(observation.Character,observation.Bill,observation.Weekly,observation.At,observation.State);
                completionHistory=recovered;completionLoadPending=false;HistorySaveError=null;
                pendingCompletionObservations=null;pendingCompletionSnapshots=null;
            }
            catch(Exception e){nextHistorySave=Now+10000;HistorySaveError="История выполнения временно недоступна для чтения. Исходный файл сохранён.";Service.PluginLog.Debug(e,"Повторное чтение истории");return;}
        }
        if(completionLoadPending || !History.Dirty || completionFile==null || (!force && Now<nextHistorySave)) return;
        try { History.Save(completionFile);HistorySaveError=null; }
        catch(Exception e) { nextHistorySave=Now+10000;HistorySaveError="Не удалось сохранить отметки выполнения. В игре зачёт сохранён, но отметки могут пропасть после перезапуска.";Service.PluginLog.Error(e,"Не удалось сохранить историю выполнения охоты"); }
    }
    public void Dispose() {
        SaveJourney(true);
        SaveHistory(true);
        Service.ClientState.Logout -= OnLogout;
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Service.Framework.Update -= Update; Service.PluginInterface.UiBuilder.Draw -= Elite.DrawWorld; Service.PluginInterface.UiBuilder.Draw -= windows.Draw; Service.PluginInterface.UiBuilder.Draw -= NativeDialogs.DrawFallback;
        Service.PluginInterface.UiBuilder.OpenMainUi -= Open; Service.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Service.Commands.RemoveHandler("/huntroute"); windows.RemoveAllWindows();try{NativeMain?.Dispose();}finally{NativeDialogs.Dispose();}
    }
}

