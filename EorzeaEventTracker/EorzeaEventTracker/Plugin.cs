using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
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
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static INotificationManager Notifications { get; private set; } = null!;

    internal Configuration Settings { get; }
    internal CharacterSettings Preferences { get; private set; } = new();
    private ulong activeCharacter;
    internal bool HasCharacterPreferences => activeCharacter != 0;
    internal EventFeedService Feed { get; }
    internal CharacterProgress Progress { get; }
    internal Localizer Locale { get; private set; }
    internal EventImages Images { get; }
    private readonly WindowSystem windows = new("EorzeaEventTracker");
    private readonly EventWindow window;
    private readonly CompactWidget widget;
    private Task? loginRefresh;
    private DateTime nextFeedRefresh;
    private DateTimeOffset nextReminderCheck;
    private bool widgetOpenedManually;
    internal bool AllowWidgetDisplay => widgetOpenedManually || ShouldAutoOpen();
    private bool ShouldAutoOpen() => loginRefresh == null && EventCompletionPolicy.ShouldAutoOpen(new EventFeed { Events = Feed.Feed.Events.Where(e => !IsEventHidden(e)).ToList() }, Progress.Catalog,
        Progress.Quests, Progress.ReadyForReminders, Preferences.ShowUnverified, DateTimeOffset.UtcNow);

    public Plugin()
    {
        Settings = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Preferences = Settings.NewCharacterDefaults();
        Locale = new Localizer(Preferences);
        if (Settings.Version < 3)
        {
            if (Settings.FeedUrl == Configuration.LegacyFeedUrl || (Settings.Version < 2 && string.IsNullOrWhiteSpace(Settings.FeedUrl)))
                Settings.FeedUrl = Configuration.DefaultFeedUrl;
            Settings.Version = 3;
            Save();
        }
        if (Settings.Version < 4) { Settings.Version = 4; Save(); }
        var data = PluginInterface.GetPluginConfigDirectory();
        var bundled = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "events.json");
        Feed = new EventFeedService(Path.Combine(data, "events.json"), bundled);
        Images = new EventImages(Path.Combine(data, "event-images"));
        Progress = new CharacterProgress(this);
        window = new EventWindow(this);
        widget = new CompactWidget(this) { IsOpen = false };
        windows.AddWindow(window);
        windows.AddWindow(widget);
        Commands.AddHandler("/events", new CommandInfo((_, args) =>
        {
            if (args.Trim().Equals("widget", StringComparison.OrdinalIgnoreCase)) ToggleWidget();
            else window.Toggle();
        }) { HelpMessage = "Open Eorzea Event Tracker; /events widget toggles the compact widget" });
        Commands.AddHandler("/eventswidget", new CommandInfo((_, _) => ToggleWidget())
        { HelpMessage = "Toggle the compact Eorzea Event Tracker widget" });
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += Open;
        PluginInterface.UiBuilder.OpenConfigUi += Open;
        Framework.Update += OnUpdate;
    }

    private void Open() => window.IsOpen = true;
    private void ToggleWidget() => SetWidgetVisible(!(widget.IsOpen && AllowWidgetDisplay));
    internal void SetWidgetVisible(bool visible)
    {
        widget.IsOpen = visible;
        widgetOpenedManually = visible;
        Preferences.ShowCompactWidget = visible;
        Save();
    }
    internal void OpenEvent(string eventId)
    {
        window.SelectEvent(eventId);
        window.IsOpen = true;
    }
    internal bool IsEventHidden(EventInfo e) => Preferences.IsEventHidden(e);
    internal void SetEventHidden(EventInfo e, bool hidden)
    {
        Preferences.SetEventHidden(e, hidden);
        Save();
    }
    internal void Save() => PluginInterface.SavePluginConfig(Settings);
    private void OnUpdate(IFramework framework)
    {
        var loggedIn = Client.IsLoggedIn;
        var character = loggedIn ? Player.ContentId : 0;
        if (loggedIn && character == 0) character = activeCharacter; // Loading a zone is not a character switch.
        var characterChanged = character != activeCharacter;
        if (characterChanged)
        {
            activeCharacter = character;
            Preferences = character == 0 ? Settings.NewCharacterDefaults() : Settings.ForCharacter(character);
            Locale = new Localizer(Preferences);
            widgetOpenedManually = false;
            nextReminderCheck = DateTimeOffset.MinValue;
            window.SwitchCharacter(character);
            widget.SwitchCharacter(character);
            if (character != 0) Save();
        }
        Progress.Update();
        if (loggedIn && character != 0 && characterChanged)
        {
            widgetOpenedManually = false;
            loginRefresh = Feed.RefreshAsync(Settings.FeedUrl);
            nextFeedRefresh = DateTime.UtcNow.AddMinutes(30);
            Images.RetryFailed();
        }
        else if (loggedIn && DateTime.UtcNow >= nextFeedRefresh)
        {
            nextFeedRefresh = DateTime.UtcNow.AddMinutes(30);
            _ = Feed.RefreshAsync(Settings.FeedUrl);
            Images.RetryFailed();
        }
        if (!loggedIn)
        {
            loginRefresh = null;
            widgetOpenedManually = false;
            window.IsOpen = false;
        }
        if (loggedIn && loginRefresh?.IsCompleted == true && Progress.ReadyForReminders)
        {
            loginRefresh = null;
            var active = Feed.Feed.Events.Count(e => e.Active(DateTimeOffset.UtcNow) && !IsEventHidden(e) && (Preferences.ShowUnverified || e.Verified));
            if (Preferences.NotifyOnLogin && active > 0)
                Chat.Print("[Eorzea Event Tracker] " + Locale.Format("{0} active event(s)! Type /events for details.", active));
            if (Preferences.OpenOnLogin && ShouldAutoOpen()) Open();
        }
        CheckDeadlineReminders();
    }

    private void CheckDeadlineReminders()
    {
        var now = DateTimeOffset.UtcNow;
        if (!Preferences.NotifyFavoriteDeadlines || !Client.IsLoggedIn || !Progress.ReadyForReminders
            || loginRefresh != null || now < nextReminderCheck) return;
        nextReminderCheck = now.AddMinutes(1);
        var changed = false;
        foreach (var e in Feed.Feed.Events.Where(e => !IsEventHidden(e) && (Preferences.ShowUnverified || e.Verified)))
        {
            var pending = DeadlineReminderPolicy.Pending(e, Progress.Catalog.GetValueOrDefault(e.Id),
                Preferences.FavoriteRewards, Progress.Rewards, Settings.DeadlineRemindersSent,
                Player.ContentId, Progress.ReadyForReminders, now, Preferences.IgnoredRewards);
            if (pending is not { } reminder) continue;
            var deadline = Locale.Text(reminder.Stage == 2 ? "Ends within 24 hours" : "Ends within 3 days");
            var message = Locale.Text(e.Title, e) + " — " + deadline + ". "
                + Locale.Format("Favorite rewards still unconfirmed: {0}. Open /events.", reminder.RemainingFavorites);
            Notifications.AddNotification(new Notification
            {
                Title = "Eorzea Event Tracker",
                Content = message,
                Type = NotificationType.Warning,
                InitialDuration = TimeSpan.FromSeconds(15),
            });
            Chat.Print("[Eorzea Event Tracker] " + message);
            Settings.DeadlineRemindersSent[reminder.Key] = reminder.Stage;
            changed = true;
        }
        if (changed) Save();
    }

    public void Dispose()
    {
        Progress.Flush();
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Open;
        PluginInterface.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/events");
        Commands.RemoveHandler("/eventswidget");
        windows.RemoveAllWindows();
        window.Dispose();
        Feed.Dispose();
        Images.Dispose();
    }
}
