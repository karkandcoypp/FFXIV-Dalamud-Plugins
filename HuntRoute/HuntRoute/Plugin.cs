using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace HuntRoute;

[Serializable] public class Settings : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public Dictionary<string, DateTimeOffset> Collected { get; set; } = new();
}
public record Board(string Name, byte[] Daily, byte Weekly, uint Aetheryte, uint Territory, uint Map, Vector2 Position);
public record EntryPoint(uint Id, string Name, Vector2 Position);
public record ZoneRoute(string Name, List<MobHuntEntry> Mobs, EntryPoint? Start);

public sealed class Plugin : IDalamudPlugin {
    public static readonly Board[] Boards = [
        new("Heavensward", [1,2,3], 5, 70, 418, 218, new(12.7f,11.8f)),
        new("Stormblood", [6,7,8], 9, 111, 628, 370, new(10.4f,10.2f)),
        new("Shadowbringers", [10,11,12], 13, 133, 819, 497, new(9.5f,9.4f)),
        new("Endwalker", [14,15,16], 17, 183, 963, 694, new(11.8f,13.2f)),
        new("Dawntrail", [18,19,20], 21, 216, 1185, 855, new(13.8f,13.6f))
    ];
    internal Settings Config;
    internal List<MobHuntEntry> Mobs = [];
    internal List<ZoneRoute> Routes = [];
    internal string? Error;
    internal bool Ready;
    internal Dictionary<(byte Bill, byte Mob), int> KillCounts = new();
    internal HashSet<byte> UnlockedBills = new();
    private readonly WindowSystem windows = new("HuntRoute");
    private readonly HuntWindow window;
    internal readonly EliteWindow Elite;
    private long nextScan;
    private string signature = "";
    private ulong character;
    private readonly Dictionary<byte, (byte Id, bool Held)> observed = new();
    internal bool HideDone = true;
    public Plugin(IDalamudPluginInterface pi) {
        pi.Create<Service>(); Config = pi.GetPluginConfig() as Settings ?? new();
        window = new(this); windows.AddWindow(window);
        Elite = new(this); windows.AddWindow(Elite);
        Service.Commands.AddHandler("/huntroute", new CommandInfo((_, _) => window.Toggle()) { HelpMessage = "Hunt Route: маршруты охоты и сброс контрактов" });
        pi.UiBuilder.Draw += windows.Draw; pi.UiBuilder.Draw += Elite.DrawWorld; pi.UiBuilder.OpenMainUi += Open; pi.UiBuilder.OpenConfigUi += Open;
        Service.Framework.Update += Update;
    }
    private void Open() => window.IsOpen = true;
    internal void Refresh() { nextScan = 0; signature = ""; }
    private unsafe void Update(IFramework _) {
        if (!Service.ClientState.IsLoggedIn || !Service.Player.IsLoaded || Service.Player.ContentId == 0) {
            Ready = false; character = 0; observed.Clear(); signature = ""; Mobs.Clear(); Routes.Clear(); KillCounts.Clear(); UnlockedBills.Clear(); Elite.Selected=null; return;
        }
        if (Environment.TickCount64 < nextScan) return; nextScan = Environment.TickCount64 + 1000;
        try {
            if (character != Service.Player.ContentId) { character = Service.Player.ContentId; observed.Clear(); signature = ""; }
            var hunt = MobHunt.Instance(); if (hunt == null) { Ready=false; return; }
            UnlockedBills = Enumerable.Range(0,22).Select(i=>(byte)i).Where(i=>hunt->IsMarkBillUnlocked(i)).ToHashSet();
            for(byte bill=0;bill<22;bill++) for(byte mob=0;mob<5;mob++) KillCounts[(bill,mob)]=hunt->GetKillCount(bill,mob);
            string current = hunt->ObtainedFlags + ":" + string.Join(",", Enumerable.Range(0,22).Select(i => hunt->ObtainedMarkId[i])) + ":" + string.Join(",", Service.Aetherytes.Select(a=>a.AetheryteId).Order());
            if (current != signature) { ReadBills(); BuildRoutes(); signature = current; }
            // Obtained IDs remain after completion. Record collection only when the game's available ID agrees.
            bool changed = false;
            foreach (var board in Boards) foreach (byte bill in board.Daily.Append(board.Weekly)) {
                if (!hunt->IsMarkBillUnlocked(bill)) continue;
                byte id = hunt->ObtainedMarkId[bill];
                bool held = (hunt->ObtainedFlags & (1 << bill)) != 0;
                bool pickup = observed.TryGetValue(bill, out var old) && held && (!old.Held || old.Id != id);
                observed[bill] = (id, held);
                if (!pickup || id == 0 || id != hunt->AvailableMarkId[bill]) continue;
                var period = ResetClock.Period(DateTimeOffset.UtcNow, bill == board.Weekly);
                string key = $"{character}:{bill}";
                if (Config.Collected.GetValueOrDefault(key) != period) { Config.Collected[key] = period; changed = true; }
            }
            if (changed) Service.PluginInterface.SavePluginConfig(Config);
            Ready = true; Error = null;
        } catch (Exception e) { Ready = false; Error = e.Message; nextScan=Environment.TickCount64+10000; Service.PluginLog.Error(e, "Reading hunt bills"); }
    }
    private unsafe void ReadBills() {
        var result = new List<MobHuntEntry>(); var hunt = MobHunt.Instance();
        var orders = Service.DataManager.GetSubrowExcelSheet<MobHuntOrder>();
        for (byte bill = 0; bill < 22; bill++) {
            if ((hunt->ObtainedFlags & (1 << bill)) == 0 || hunt->ObtainedMarkId[bill] == 0) continue;
            var type = Service.DataManager.GetExcelSheet<MobHuntOrderType>().GetRow(bill);
            uint row = type.OrderStart.RowId + hunt->ObtainedMarkId[bill] - 1u;
            foreach (var order in orders[row]) {
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
        foreach (var marker in Service.DataManager.GetSubrowExcelSheet<MapMarker>()[map.MapMarkerRange]) {
            if (marker.DataType != 3 || !unlocked.Contains(marker.DataKey.RowId)) continue;
            var a = Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(marker.DataKey.RowId);
            if (a.IsAetheryte && a.Territory.RowId == territory) result.Add(new(a.RowId, a.PlaceName.Value.Name.ToString(), Location.ConvertPixelPositionToMapCoordinate(marker.X, marker.Y, map.SizeFactor)));
        }
        // Hinterlands can only be entered via Idyllshire; do not fabricate an in-zone distance.
        return result;
    }
    private void BuildRoutes() {
        var result = new List<ZoneRoute>();
        foreach (var group in Mobs.GroupBy(m => (m.TerritoryType, m.MapId)).OrderBy(g=>g.Key.TerritoryType)) {
            var known = group.Where(m => !m.IsEliteMark && Location.Database.ContainsKey(m.MobHuntId)).OrderBy(m=>m.MobHuntId).ThenBy(m=>m.BillNumber).ToList();
            var entries = Entries(group.Key.MapId, group.Key.TerritoryType);
            var (order, start) = RouteSolver.Solve(known.Select(m=>Location.Database[m.MobHuntId].Coordinate).ToArray(), entries.Select(e=>e.Position).ToArray());
            result.Add(new(group.First().TerritoryName ?? "Unknown", order.Select(i=>known[i]).Concat(group.Except(known)).ToList(), start < 0 ? null : entries[start]));
        }
        Routes = result;
    }
    internal int Kills(MobHuntEntry m) => KillCounts.GetValueOrDefault((m.BillNumber,m.MobIndex));
    internal bool Done(MobHuntEntry m) => Kills(m) >= m.NeededKills;
    internal unsafe void Teleport(uint id) {
        if (!Ready || !Service.Aetherytes.Any(a=>a.AetheryteId==id)) { Service.Chat.PrintError("Эфирит не открыт."); return; }
        if (Service.Condition[ConditionFlag.InCombat] || Service.Condition[ConditionFlag.BetweenAreas] || Service.Condition[ConditionFlag.BetweenAreas51] || Service.ObjectTable.LocalPlayer?.IsCasting == true) { Service.Chat.PrintError("Телепорт сейчас недоступен."); return; }
        var telepo = Telepo.Instance();
        if (telepo == null || !telepo->Teleport(id, 0)) Service.Chat.PrintError("Игра отклонила телепорт.");
    }
    internal void Travel(MobHuntEntry m) {
        if (m.IsEliteMark || !Location.Database.TryGetValue(m.MobHuntId, out var pos)) return;
        var entry = Entries(m.MapId,m.TerritoryType).MinBy(a=>Vector2.DistanceSquared(a.Position,pos.Coordinate));
        if (entry != null) Teleport(entry.Id);
        else if (m.TerritoryType == 399) Teleport(75);
        else Service.Chat.PrintError("Нет открытого эфирита на этой карте.");
        Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name);
    }
    internal (int Collected,int Total) CollectionProgress(Board b,bool weekly) {
        if(!Ready) return (0,0);
        var indices=weekly?new[]{b.Weekly}:b.Daily;
        var unlocked=indices.Where(i=>UnlockedBills.Contains(i)).ToArray();
        var period=ResetClock.Period(DateTimeOffset.UtcNow,weekly);
        return (unlocked.Count(i=>Config.Collected.GetValueOrDefault($"{character}:{i}")>=period),unlocked.Length);
    }
    public void Dispose() {
        Service.Framework.Update -= Update; Service.PluginInterface.UiBuilder.Draw -= Elite.DrawWorld; Service.PluginInterface.UiBuilder.Draw -= windows.Draw;
        Service.PluginInterface.UiBuilder.OpenMainUi -= Open; Service.PluginInterface.UiBuilder.OpenConfigUi -= Open;
        Service.Commands.RemoveHandler("/huntroute"); windows.RemoveAllWindows();
    }
}

