using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BagSense;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;
    [PluginService] internal static IGameInventory Inventory { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IContextMenu Context { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    internal Configuration Settings { get; }
    internal Text Locale { get; }
    internal ItemCatalog? Catalog { get; private set; }
    internal InventorySnapshot? Snapshot => Demo ? demoSnapshot : liveSnapshot;
    internal CharacterData? Current => Demo ? demoData : liveSnapshot == null ? null : Settings.ForCharacter(liveSnapshot.CharacterId);
    internal bool Demo { get; private set; }
    internal bool ReadError { get; private set; }
    internal bool CatalogError { get; private set; }
    internal bool MapFailed { get; private set; }
    private readonly WindowSystem windows = new("BagSense");
    private readonly BagWindow window;
    private InventorySnapshot? liveSnapshot;
    private InventorySnapshot demoSnapshot;
    private CharacterData demoData;
    private Task<ItemCatalog>? catalogTask;
    private Task<DatabaseResult>? databaseTask;
    private readonly CancellationTokenSource lifetime = new();
    private DateTimeOffset nextDatabaseCheck;
    internal bool DatabaseBusy => databaseTask != null;
    internal bool DatabaseOffline { get; private set; }
    internal void RefreshDatabase() => nextDatabaseCheck = DateTimeOffset.MinValue;
    private ulong characterId;
    private DateTimeOffset nextScan;
    private DateTimeOffset nextErrorLog;

    public Plugin()
    {
        Settings = Pi.GetPluginConfig() as Configuration ?? new();
        Settings.Scale = Math.Clamp(Settings.Scale, .85f, 1.5f);
        Locale = new(() => Settings.Russian);
        (demoSnapshot, demoData) = DemoData.Create();
        window = new(this);
        windows.AddWindow(window);
        Commands.AddHandler("/bagsense", new CommandInfo(OnCommand) { HelpMessage = "Open BagSense / открыть BagSense. /bagsense demo: sample inventory." });
        Pi.UiBuilder.Draw += windows.Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        Context.OnMenuOpened += OnMenuOpened;
    }

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("demo", StringComparison.OrdinalIgnoreCase)) SetDemo(true);
        else if (arguments.Trim().Equals("live", StringComparison.OrdinalIgnoreCase)) SetDemo(false);
        window.IsOpen = true;
    }
    private void Open() => window.IsOpen = true;
    internal void Save() => Pi.SavePluginConfig(Settings);
    internal void Changed() { if (!Demo) Save(); }
    internal void Refresh() { nextScan = DateTimeOffset.MinValue; CatalogError = false; }
    internal void OpenMap(NpcLocation location)
    {
        if (Demo || !location.CanMap || !Client.IsLoggedIn) return;
        try { MapFailed = !GameGui.OpenMapWithMapLink(new MapLinkPayload(location.Territory, location.Map, location.X, location.Y)); }
        catch (Exception ex) { MapFailed = true; Log.Warning(ex, "BagSense: could not open NPC map"); }
    }
    internal void SetDemo(bool demo) { Demo = demo; window.ResetSelection(); Refresh(); }
    internal void ResetDemo() { (demoSnapshot, demoData) = DemoData.Create(); window.ResetSelection(); }

    private void Update(IFramework framework)
    {
        bool loaded = Client.IsLoggedIn && Player.IsLoaded && Player.ContentId != 0;
        ulong id = loaded ? Player.ContentId : 0;
        if (characterId != id)
        {
            characterId = id;
            liveSnapshot = null;
            ReadError = false;
            nextScan = DateTimeOffset.UtcNow.AddSeconds(5);
            if (!Demo) window.ResetSelection();
        }
        if (Catalog == null && catalogTask == null && !CatalogError)
            catalogTask = Task.Run(() => new ItemCatalog(Data.Excel, path => Data.GameData.GetFile<Lumina.Data.Files.LgbFile>(path)));
        if (catalogTask?.IsCompleted == true)
        {
            if (catalogTask.IsCompletedSuccessfully) Catalog = catalogTask.Result;
            else { CatalogError = true; Log.Error(catalogTask.Exception!, "BagSense: item catalog failed"); }
            catalogTask = null;
        }
        if (Catalog != null && databaseTask == null && DateTimeOffset.UtcNow >= nextDatabaseCheck)
        {
            nextDatabaseCheck = DateTimeOffset.UtcNow.AddHours(24);
            databaseTask = DatabaseCatalog.LoadAsync(Path.Combine(Pi.ConfigDirectory.FullName, "database"), lifetime.Token);
        }
        if (databaseTask?.IsCompleted == true)
        {
            if (databaseTask.IsCompletedSuccessfully)
            {
                var result = databaseTask.Result;
                DatabaseOffline = result.Offline;
                if (result.Document != null) { Catalog!.ApplyDatabase(result.Document); nextScan = DateTimeOffset.MinValue; }
                if (result.Offline) Log.Warning("BagSense: online catalog unavailable ({Reason}); using cached/installed data", result.Error);
            }
            else if (!databaseTask.IsCanceled) Log.Warning(databaseTask.Exception!, "BagSense: catalog update failed");
            databaseTask = null;
        }
        if (!loaded || Catalog == null || DateTimeOffset.UtcNow < nextScan) return;
        nextScan = DateTimeOffset.UtcNow.AddSeconds(3);
        try
        {
            var snapshot = InventoryReader.Read(Catalog);
            var data = Settings.ForCharacter(snapshot.CharacterId);
            bool changed = false;
            foreach (var step in data.Plan)
            {
                if (!step.ObservedDecrease && snapshot.Quantity(step.ItemId) < step.BaselineQuantity)
                { step.ObservedDecrease = true; changed = true; }
            }
            liveSnapshot = snapshot;
            ReadError = false;
            if (changed) Save();
        }
        catch (Exception ex)
        {
            liveSnapshot = null;
            ReadError = true;
            if (DateTimeOffset.UtcNow >= nextErrorLog)
            { Log.Warning(ex, "BagSense: inventory unavailable"); nextErrorLog = DateTimeOffset.UtcNow.AddMinutes(1); }
        }
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.Target is not MenuTargetInventory target || target.TargetItem is not { } item) return;
        uint id = item.BaseItemId;
        if (liveSnapshot == null || !liveSnapshot.Items.ContainsKey(id)) return;
        ulong owner = liveSnapshot.CharacterId;
        args.AddMenuItem(new MenuItem
        {
            Name = Locale.T("BagSense: зачем храню?", "BagSense: why keep this?"),
            OnClicked = _ =>
            {
                if (!Client.IsLoggedIn || Player.ContentId != owner || liveSnapshot?.CharacterId != owner) return;
                SetDemo(false);
                window.Select(id);
                window.IsOpen = true;
            },
        });
    }

    public void Dispose()
    {
        lifetime.Cancel();
        Context.OnMenuOpened -= OnMenuOpened;
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= windows.Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/bagsense");
        windows.RemoveAllWindows();
        window.Dispose();
    }
}
