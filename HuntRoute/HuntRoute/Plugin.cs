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
public record ZoneRoute(string Name, List<MobHuntEntry> Mobs, EntryPoint? Start, bool StartsHere=false);

public sealed class Plugin : IDalamudPlugin {
    public static readonly Board[] Boards = [
        new("Heavensward", [1,2,3], 5, 70, 418, 218, new(12.7f,11.8f)),
        new("Stormblood", [6,7,8], 9, 111, 628, 370, new(10.4f,10.2f)),
        new("Shadowbringers", [10,11,12], 13, 133, 819, 497, new(9.5f,9.4f)),
        new("Endwalker", [14,15,16], 17, 183, 963, 694, new(11.8f,13.2f)),
        new("Dawntrail", [18,19,20], 21, 216, 1185, 855, new(13.8f,13.6f))
    ];
    internal List<MobHuntEntry> Mobs = [];
    internal List<ZoneRoute> Routes = [];
    internal string? Error;
    internal bool Ready;
    internal Dictionary<(byte Bill, byte Mob), int> KillCounts = new();
    internal Dictionary<byte,BillState> BillStates = new();
    private readonly WindowSystem windows = new("HuntRoute");
    private readonly HuntWindow window;
    internal readonly EliteWindow Elite;
    internal readonly NextZoneWindow NextZone;
    internal readonly ShortcutWindow Shortcut;
    internal readonly NativeDialogService NativeDialogs;
    private bool wasLoading;
    private long nextScan;
    private string signature = "";
    private ulong character;
    private string routeSignature="";
    private long nextTeleportAllowed;
    internal uint CurrentTerritory;
    internal uint CurrentMap;
    internal bool HideDone = true;
    internal readonly RouteSession Session = new();
    public Plugin(IDalamudPluginInterface pi) {
        pi.Create<Service>();
        NativeDialogs=new(pi);
        window = new(this); windows.AddWindow(window);
        Elite = new(this); windows.AddWindow(Elite);
        NextZone=new(this);
        Shortcut=new(this);
        Service.Commands.AddHandler("/huntroute", new CommandInfo((_, _) => window.Toggle()) { HelpMessage = "Hunt Route: маршруты охоты и сброс контрактов" });
        pi.UiBuilder.Draw += windows.Draw; pi.UiBuilder.Draw += Elite.DrawWorld; pi.UiBuilder.OpenMainUi += Open; pi.UiBuilder.OpenConfigUi += Open;
        Service.Framework.Update += Update;
        Service.ClientState.Logout += OnLogout;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
    }
    private void Open() => window.IsOpen = true;
    internal void Refresh() { nextScan = 0; signature = ""; }
    private void OnLogout(int type,int code) {
        Ready=false; character=0; signature=""; routeSignature=""; nextScan=0; nextTeleportAllowed=0;
        wasLoading=false; CurrentTerritory=0; CurrentMap=0; Mobs.Clear(); Routes.Clear(); KillCounts.Clear(); BillStates.Clear();
        Elite.Selected=null; Session.Stop(); NextZone.Sync(); Shortcut.Sync();
    }
    private void OnTerritoryChanged(uint territory) {
        nextScan=0; routeSignature=""; Session.ZoneTransition();
    }
    private unsafe void Update(IFramework _) {
        // Loading can temporarily clear LocalPlayer/ContentId. Only the logout event clears the route.
        bool loading=Service.Condition.Any(ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51);
        if(loading) { if(!wasLoading) Session.ZoneTransition(); wasLoading=true; return; }
        if(wasLoading) { wasLoading=false;nextScan=0;routeSignature="";Session.ZoneTransition(); }
        if(!Service.ClientState.IsLoggedIn || !Service.Player.IsLoaded || Service.Player.ContentId==0) {
            Ready=false; return;
        }
        if (Environment.TickCount64 < nextScan) return; nextScan = Environment.TickCount64 + 1000;
        try {
            if (character != Service.Player.ContentId) { character = Service.Player.ContentId; signature = ""; routeSignature=""; nextTeleportAllowed=0; Elite.Selected=null; Session.Stop(); NextZone.Sync(); Shortcut.Sync(); }
            var hunt = MobHunt.Instance(); if (hunt == null) { Ready=false; return; }
            for(byte bill=0;bill<22;bill++) for(byte mob=0;mob<5;mob++) KillCounts[(bill,mob)]=hunt->GetKillCount(bill,mob);
            string current = hunt->ObtainedFlags + ":" + string.Join(",", Enumerable.Range(0,22).Select(i => hunt->ObtainedMarkId[i])) + ":" + string.Join(",", Service.Aetherytes.Select(a=>a.AetheryteId).Order());
            if (current != signature) {
                ReadBills(); signature = current; routeSignature="";
                if(Elite.Selected is {} old) Elite.Selected=Mobs.FirstOrDefault(m=>m.BillNumber==old.BillNumber && m.MobIndex==old.MobIndex && m.MobHuntId==old.MobHuntId);
            }
            CurrentTerritory=Service.ClientState.TerritoryType; CurrentMap=Service.ClientState.MapId;
            string routeKey=current+":"+CurrentTerritory+":"+CurrentMap+":"+string.Join(",",Mobs.Where(Done).Select(m=>$"{m.BillNumber}-{m.MobIndex}"));
            if(routeSignature!=routeKey) { BuildRoutes(); routeSignature=routeKey; }
            var entries=Mobs.ToLookup(m=>m.BillNumber);
            var snapshots=new Dictionary<byte,BillState>();
            for(byte bill=0;bill<22;bill++) {
                bool obtained=(hunt->ObtainedFlags & (1<<bill))!=0;
                bool complete=entries[bill].Any() && entries[bill].All(Done);
                var type=Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(bill);
                snapshots[bill]=BillState.FromGame(hunt->IsMarkBillUnlocked(bill),obtained,hunt->ObtainedMarkId[bill],hunt->AvailableMarkId[bill],complete,type.OrderAmount);
            }
            BillStates=snapshots;
            Ready = true; Error = null; AdvanceRoute(); NativeDialogs.Pump();
        } catch (Exception e) { Ready = false; Error = e.Message; nextScan=Environment.TickCount64+10000; Service.PluginLog.Error(e, "Reading hunt bills"); }
    }
    private unsafe void ReadBills() {
        var result = new List<MobHuntEntry>(); var hunt = MobHunt.Instance();
        var orders = Service.DataManager.GetSubrowExcelSheet<MobHuntOrder>();
        for (byte bill = 0; bill < 22; bill++) {
            if ((hunt->ObtainedFlags & (1 << bill)) == 0 || hunt->ObtainedMarkId[bill] == 0) continue;
            var type = Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(bill);
            if(hunt->ObtainedMarkId[bill]>type.OrderAmount) continue;
            uint row = type.OrderStart.RowId + hunt->ObtainedMarkId[bill] - 1u;
            if(!orders.TryGetRow(row,out var orderRows)) continue;
            foreach (var order in orderRows) {
                if(order.Target.RowId==0 || order.NeededKills==0 || order.SubrowId>=5) continue;
                var target = order.Target.Value; var map = target.Map.Value;
                result.Add(new MobHuntEntry { Name = target.Name.Value.Singular.ToString(), TerritoryName = map.PlaceName.Value.Name.ToString(),
                    ExpansionName = map.TerritoryType.Value.ExVersion.Value.Name.ToString(), ExpansionId = map.TerritoryType.Value.ExVersion.RowId,
                    TerritoryType = map.TerritoryType.RowId, MapId = map.RowId, MobHuntId = target.Name.RowId,
                    IsEliteMark = type.Type == 2, BillNumber = bill, MobIndex = (byte)order.SubrowId, NeededKills = order.NeededKills, Icon = target.Icon });
            }
        }
        Mobs = result;
    }
    internal List<EntryPoint> Entries(uint mapId, uint territory) {
        var map = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(mapId);
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
    private void BuildRoutes() {
        var result = new List<ZoneRoute>();
        foreach (var group in Mobs.GroupBy(m => (m.TerritoryType, m.MapId)).OrderBy(g=>g.Key.TerritoryType)) {
            var targets=group.OrderBy(m=>m.MobHuntId).ThenBy(m=>m.BillNumber).ToList();
            var entries=Entries(group.Key.MapId,group.Key.TerritoryType);
            bool here=CurrentTerritory==group.Key.TerritoryType;
            Vector2[] origins=entries.Select(e=>e.Position).ToArray();
            if(here && CurrentMap==group.Key.MapId && Service.ObjectTable.LocalPlayer is {} player) {
                var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(group.Key.MapId);
                origins=[Dalamud.Utility.MapUtil.WorldToMap(new Vector2(player.Position.X,player.Position.Z),map)];
            }
            var (order,start)=RoutePlanner.Order(targets,Done,m=>!m.IsEliteMark && Location.Database.TryGetValue(m.MobHuntId,out var p)?p.Coordinate:null,origins);
            EntryPoint? entry=!here && start>=0?entries[start]:null;
            if(!here && entry==null && group.Key.TerritoryType==399 && Service.Aetherytes.Any(a=>a.AetheryteId==75))
                entry=new(75,"Idyllshire (переход в зону)",Vector2.Zero);
            result.Add(new(group.First().TerritoryName ?? "Unknown",order.ToList(),entry,here));
        }
        Routes = result;
    }
    internal int Kills(MobHuntEntry m) => Math.Clamp(KillCounts.GetValueOrDefault((m.BillNumber,m.MobIndex)),0,(int)Math.Min(m.NeededKills,(uint)int.MaxValue));
    internal bool Done(MobHuntEntry m) => Kills(m) >= m.NeededKills;
    internal bool TravelBusy => Service.Condition.Any(ConditionFlag.InCombat,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,
            ConditionFlag.Unconscious,ConditionFlag.Occupied,ConditionFlag.OccupiedInEvent,ConditionFlag.OccupiedInQuestEvent,
            ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.WatchingCutscene,ConditionFlag.Casting)
            || Service.ObjectTable.LocalPlayer?.IsCasting==true || Environment.TickCount64<nextTeleportAllowed;
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
        var telepo=Telepo.Instance();
        if(telepo==null || telepo->ActiveTeleportRequest || !telepo->Teleport(id,0)) { Service.Chat.PrintError("Игра отклонила телепорт или уже выполняет его."); return false; }
        nextTeleportAllowed=Environment.TickCount64+3000;
        return true;
    }
    internal void Travel(MobHuntEntry m) {
        if(!Ready || !Service.ClientState.IsLoggedIn || m.IsEliteMark || !Location.Database.TryGetValue(m.MobHuntId,out var pos)) return;
        if(Done(m)) { Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name); return; }
        if(Service.ClientState.TerritoryType!=m.TerritoryType) {
            var entry=Entries(m.MapId,m.TerritoryType).MinBy(a=>Vector2.DistanceSquared(a.Position,pos.Coordinate));
            if(entry!=null) { if(!Teleport(entry.Id)) return; }
            else if(m.TerritoryType==399) { if(!Teleport(75)) return; }
            else { Service.Chat.PrintError("Нет открытого эфирита на этой карте."); return; }
        }
        var board=Boards.FirstOrDefault(b=>b.Daily.Contains(m.BillNumber));
        if(board!=null) { Session.Start(m,board.Daily); AdvanceRoute(); }
        else Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name);
    }
    internal void AdvanceRoute() {
        if(!Ready || Service.Condition.Any(ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.Casting) || Environment.TickCount64<nextTeleportAllowed) return;
        var mark=Session.Tick(Routes.SelectMany(r=>r.Mobs).ToArray(),Done,m=>Location.Database.ContainsKey(m.MobHuntId),Service.ClientState.TerritoryType);
        if(mark!=null && !Location.SetMapFlag(mark.TerritoryType,mark.MapId,mark.MobHuntId)) Session.RetryMarker();
        NextZone.Sync(); Shortcut.Sync();
    }
    internal ShortcutOffer? FindShortcut() {
        if(!Ready || Session.Phase!=RoutePhase.Following || Session.Target is not {} target || TravelBusy
            || Service.ClientState.TerritoryType!=target.TerritoryType || Service.ClientState.MapId!=target.MapId
            || Service.ObjectTable.LocalPlayer is not {} player || Done(target)
            || !Location.Database.TryGetValue(target.MobHuntId,out var position)) return null;
        var map=Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(target.MapId);
        var destination=MapCoordinates.ToWorld(position.Coordinate,map);
        var here=new Vector2(player.Position.X,player.Position.Z);
        if(Vector2.Distance(here,destination)<ShortcutAdvisor.MinimumDistance) return null;
        var prices=Service.Aetherytes.GroupBy(a=>a.AetheryteId).ToDictionary(g=>g.Key,g=>g.Min(a=>a.GilCost));
        var candidates=Entries(target.MapId,target.TerritoryType).Where(e=>prices.ContainsKey(e.Id))
            .Select(e=>new ShortcutCandidate(e.Id,e.Name,MapCoordinates.ToWorld(e.Position,map),prices[e.Id]));
        return ShortcutAdvisor.Evaluate(Session.RunId,target,here,destination,candidates);
    }
    internal bool AcceptShortcut(ShortcutOffer offered) {
        var current=FindShortcut();
        if(current==null || current.RunId!=offered.RunId || current.Bill!=offered.Bill || current.Mob!=offered.Mob
            || current.TargetId!=offered.TargetId || current.Aetheryte!=offered.Aetheryte || current.GilCost>offered.GilCost) {
            Service.Chat.PrintError("Предложение изменилось или больше не сокращает путь. Проверьте обновлённые данные."); return false;
        }
        if(!Teleport(current.Aetheryte,true)) return false;
        Session.ZoneTransition();nextScan=0;routeSignature="";
        return true;
    }
    internal ContractProgress CollectionProgress(Board b,bool weekly) =>
        Ready ? ContractProgress.Summarize(weekly?new[]{b.Weekly}:b.Daily,BillStates) : default;
    public void Dispose() {
        Service.ClientState.Logout -= OnLogout;
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Service.Framework.Update -= Update; Service.PluginInterface.UiBuilder.Draw -= Elite.DrawWorld; Service.PluginInterface.UiBuilder.Draw -= windows.Draw;
        Service.PluginInterface.UiBuilder.OpenMainUi -= Open; Service.PluginInterface.UiBuilder.OpenConfigUi -= Open;
        Service.Commands.RemoveHandler("/huntroute"); windows.RemoveAllWindows();NativeDialogs.Dispose();
    }
}

