using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CraftProfile;
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IGameInventory Inventory { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    internal Configuration Settings { get; }
    internal ProfileDocument? Profile { get; private set; }
    internal string? Error { get; private set; }
    internal string ExportDirectory => Pi.ConfigDirectory.FullName;
    internal string LatestFile => Path.Combine(ExportDirectory, "latest.json");
    internal bool Writing => !writer.IsCompleted;
    internal bool CatalogComplete => reader.CatalogComplete;
    internal int RecipeCount => reader.RecipeCount;
    private readonly ProfileReader reader = new();
    private readonly ProfileWindow window;
    private Task writer = Task.CompletedTask;
    private string characterKey = "";
    private DateTimeOffset nextRead, nextExport, nextUnlockRefresh;
    private bool force;
    public Plugin()
    {
        Settings = Pi.GetPluginConfig() as Configuration ?? new();
        Settings.ExportSeconds = Math.Clamp(Settings.ExportSeconds, 5, 300);
        window = new(this);
        Commands.AddHandler("/craftprofile", new CommandInfo(OnCommand) { HelpMessage = "CraftProfile / профессии. /craftprofile export: local snapshot; /craftprofile refresh: recheck unlocks." });
        Pi.UiBuilder.Draw += window.Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        QueueSession(false, null, null, "Waiting for a loaded character");
    }
    private void Open() => window.IsOpen = true;
    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("refresh", StringComparison.OrdinalIgnoreCase)) { reader.RestartUnlocks(); Refresh(); }
        else if (args.Trim().Equals("export", StringComparison.OrdinalIgnoreCase)) Refresh();
        window.IsOpen = true;
    }
    internal void Refresh() { force = true; nextRead = DateTimeOffset.MinValue; }
    internal void SaveSettings()
    {
        Pi.SavePluginConfig(Settings);
        if (!Settings.AutoExport) QueueSession(false, characterKey, null, "Automatic export paused");
        else Refresh();
    }
    private void Update(IFramework framework)
    {
        bool loaded = Client.IsLoggedIn && Player.IsLoaded && Player.ContentId != 0 && Objects.LocalPlayer != null;
        string key = loaded ? Player.ContentId.ToString() : "";
        var now = DateTimeOffset.UtcNow;
        if (key != characterKey)
        {
            characterKey = key; reader.Reset(); Profile = null; Error = null;
            nextRead = now.AddSeconds(3); nextExport = now; nextUnlockRefresh = now.AddMinutes(5);
            QueueSession(false, string.IsNullOrEmpty(key) ? null : key, null, "Waiting for a stable snapshot");
            if (loaded) Profile = ProfilePolicy.Load(ProfilePath(key), key);
        }
        if (writer.IsFaulted)
        {
            Error = "Local export failed: " + writer.Exception!.GetBaseException().Message;
            Log.Warning(writer.Exception, "CraftProfile local export failed");
            writer = Task.CompletedTask;
        }
        if (!loaded || (!Settings.AutoExport && !force)) return;
        try
        {
            if (now >= nextUnlockRefresh) { reader.RestartUnlocks(); nextUnlockRefresh = now.AddMinutes(5); }
            reader.AdvanceRecipes();
            if (now < nextRead) return;
            nextRead = now.AddSeconds(1);
            Profile = reader.Read(Profile ?? new() { CharacterKey = key });
            Error = null;
            if (!Writing && (force || now >= nextExport))
            {
                force = false; nextExport = now.AddSeconds(Settings.ExportSeconds);
                var captured = Profile;
                string path = ProfilePath(key);
                string latest = LatestFile;
                var session = new SessionDocument(1, true, key, captured.GeneratedAt,
                    $"profiles/{key}/profile.json", null);
                // The captured document and its collections are not mutated by subsequent reads.
                writer = Task.Run(() => { ProfilePolicy.WriteAtomic(path, captured); ProfilePolicy.WriteAtomic(latest, session); });
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message; nextRead = now.AddSeconds(5);
            if (!Writing) QueueSession(false, key, null, Error);
        }
    }
    private string ProfilePath(string key) => Path.Combine(ExportDirectory, "profiles", key, "profile.json");
    private void QueueSession(bool loggedIn, string? key, string? profile, string? error)
    {
        string latest = LatestFile;
        var state = new SessionDocument(1, loggedIn, key, DateTimeOffset.UtcNow, profile, error);
        var previous = writer;
        writer = Task.Run(async () =>
        {
            try { await previous.ConfigureAwait(false); } catch { }
            ProfilePolicy.WriteAtomic(latest, state);
        });
    }
    public void Dispose()
    {
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= window.Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/craftprofile");
        QueueSession(false, string.IsNullOrEmpty(characterKey) ? null : characterKey, null, "Plugin unloaded");
        try { writer.GetAwaiter().GetResult(); } catch (Exception ex) { Log.Warning(ex, "CraftProfile final session status could not be saved"); }
    }
}
