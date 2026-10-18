using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace EorzeaEventTracker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;
    [PluginService] internal static IGameInventory Inventory { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;

    internal Configuration Settings { get; }
    internal EventFeedService Feed { get; }
    internal CharacterProgress Progress { get; }
    internal Localizer Locale { get; }
    private readonly WindowSystem windows = new("EorzeaEventTracker");
    private readonly EventWindow window;
    private bool previousLoggedIn;
    private Task? loginRefresh;
    private DateTime nextFeedRefresh;

    public Plugin()
    {
        Settings = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Locale = new Localizer(Settings);
        if (Settings.Version < 2)
        {
            if (string.IsNullOrWhiteSpace(Settings.FeedUrl)) Settings.FeedUrl = Configuration.DefaultFeedUrl;
            Settings.Version = 2;
            Save();
        }
        var data = PluginInterface.GetPluginConfigDirectory();
        var bundled = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "events.json");
        Feed = new EventFeedService(Path.Combine(data, "events.json"), bundled);
        Progress = new CharacterProgress(this);
        window = new EventWindow(this);
        windows.AddWindow(window);
        Commands.AddHandler("/events", new CommandInfo((_, _) => window.Toggle())
        { HelpMessage = "Open the Eorzea Event Tracker window" });
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += Open;
        PluginInterface.UiBuilder.OpenConfigUi += Open;
        Framework.Update += OnUpdate;
    }

    private void Open() => window.IsOpen = true;
    internal void Save() => PluginInterface.SavePluginConfig(Settings);
    private void OnUpdate(IFramework framework)
    {
        Progress.Update();
        var loggedIn = Client.IsLoggedIn;
        if (loggedIn && !previousLoggedIn)
        {
            loginRefresh = Feed.RefreshAsync(Settings.FeedUrl);
            nextFeedRefresh = DateTime.UtcNow.AddMinutes(30);
        }
        else if (loggedIn && DateTime.UtcNow >= nextFeedRefresh)
        {
            nextFeedRefresh = DateTime.UtcNow.AddMinutes(30);
            _ = Feed.RefreshAsync(Settings.FeedUrl);
        }
        if (!loggedIn) loginRefresh = null;
        if (loggedIn && loginRefresh?.IsCompleted == true)
        {
            loginRefresh = null;
            var active = Feed.Feed.Events.Count(e => e.Active(DateTimeOffset.UtcNow) && (Settings.ShowUnverified || e.Verified));
            if (Settings.NotifyOnLogin && active > 0)
                Chat.Print("[Eorzea Event Tracker] " + Locale.Format("{0} active event(s)! Type /events for details.", active));
            if (Settings.OpenOnLogin && active > 0) Open();
        }
        previousLoggedIn = loggedIn;
    }

    public void Dispose()
    {
        Progress.Flush();
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Open;
        PluginInterface.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/events");
        windows.RemoveAllWindows();
        window.Dispose();
        Feed.Dispose();
    }
}
