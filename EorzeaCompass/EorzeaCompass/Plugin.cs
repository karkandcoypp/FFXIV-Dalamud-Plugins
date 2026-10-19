using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using UIState = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState;
using Lumina.Excel.Sheets;

namespace EorzeaCompass;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    internal Configuration Settings { get; }
    internal Catalog? Catalog { get; private set; }
    internal IReadOnlyList<ActivityProgress> Progress { get; private set; } = Array.Empty<ActivityProgress>();
    internal string? Error { get; private set; }
    internal bool Available { get; private set; }
    internal ulong CharacterId { get; private set; }
    private static readonly HashSet<uint> EmptyIds = new();
    internal IReadOnlySet<uint> Hidden => Settings.Hidden.GetValueOrDefault(CharacterId) ?? EmptyIds;
    private readonly WindowSystem windows = new("EorzeaCompass");
    private readonly CompassWindow window;
    private DateTime scanAfter;
    private DateTime notifyAfter;
    private DateTime retryCatalogAfter;
    private bool notified;
    private Task<Catalog>? catalogTask;

    public Plugin()
    {
        Settings = Pi.GetPluginConfig() as Configuration ?? new();
        window = new(this);
        windows.AddWindow(window);
        Commands.AddHandler("/compass", new CommandInfo((_, _) => window.Toggle()) { HelpMessage = "Open Eorzea Compass / навигатор по контенту" });
        Pi.UiBuilder.Draw += windows.Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
    }

    internal string T(string ru, string en) => Settings.Russian ? ru : en;
    internal void Save() => Pi.SavePluginConfig(Settings);
    private void Open() => window.IsOpen = true;
    internal void Refresh() => scanAfter = DateTime.MinValue;
    internal void Hide(uint id, bool hide)
    {
        if (CharacterId == 0) return;
        if (!Settings.Hidden.TryGetValue(CharacterId, out var ids)) Settings.Hidden[CharacterId] = ids = new();
        if (hide) ids.Add(id); else ids.Remove(id);
        Save();
    }

    private void Update(IFramework _)
    {
        if (!Client.IsLoggedIn || !Player.IsLoaded || Player.ContentId == 0)
        {
            Available = false;
            CharacterId = 0;
            Progress = Array.Empty<ActivityProgress>();
            notified = false;
            return;
        }
        var now = DateTime.UtcNow;
        if (CharacterId != Player.ContentId)
        {
            CharacterId = Player.ContentId;
            Available = false;
            Progress = Array.Empty<ActivityProgress>();
            notified = false;
            scanAfter = now.AddSeconds(5);
            notifyAfter = now.AddSeconds(12);
        }
        if (now < scanAfter) return;
        scanAfter = now.AddSeconds(3);
        if (Catalog == null && now < retryCatalogAfter) return;
        try
        {
            if (Catalog == null)
            {
                catalogTask ??= Task.Run(() => new Catalog(Data.GetExcelSheet<Quest>(ClientLanguage.English),
                    Data.GetExcelSheet<ContentFinderCondition>(ClientLanguage.English)));
                if (!catalogTask.IsCompleted) return;
                Catalog = catalogTask.GetAwaiter().GetResult();
                catalogTask = null;
            }
            Scan();
            Error = null;
            if (!notified && Available && now >= notifyAfter
                && !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]
                && !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]
                && !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent]
                && !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene])
            {
                notified = true;
                var remaining = Visible().Count(p => p.State is ActivityState.NotStarted or ActivityState.InProgress);
                var suggestions = Suggestions().Take(3).ToArray();
                if (Settings.NotifyOnLogin && remaining > 0)
                {
                    Chat.Print($"[Eorzea Compass] {T("Ещё не открыто", "Still locked")}: {remaining}. /compass");
                    foreach (var p in suggestions)
                        Chat.Print($"[Eorzea Compass] {p.Activity.Name} → {QuestName(p.Next!.QuestId)}");
                }
                if (Settings.OpenOnLogin && remaining > 0) Open();
            }
        }
        catch (Exception e)
        {
            Available = false;
            catalogTask = null;
            Progress = Array.Empty<ActivityProgress>();
            Error = T("Не удалось прочитать прогресс. Повтор через 30 секунд; подробности в /xllog.", "Progress unavailable. Retrying in 30 seconds; see /xllog.");
            scanAfter = retryCatalogAfter = now.AddSeconds(30);
            Log.Error(e, "Eorzea Compass progress scan failed");
        }
    }

    private unsafe void Scan()
    {
        var manager = QuestManager.Instance();
        if (manager == null) { Available = false; Progress = Array.Empty<ActivityProgress>(); return; }
        var sheet = Data.GetExcelSheet<Quest>(ClientLanguage.English);
        var observations = new Dictionary<uint, QuestObservation>();
        var categories = new Dictionary<uint, bool>();
        foreach (var id in Catalog!.RelevantQuests)
        {
            if (!sheet.TryGetRow(id, out var row)) continue;
            var category = row.ClassJobCategory0.RowId;
            if (!categories.TryGetValue(category, out var allowed))
            {
                allowed = category is 0 or 1;
                if (!allowed)
                {
                    var job = Data.GetExcelSheet<ClassJob>(ClientLanguage.English).GetRow(Player.ClassJob.RowId);
                    var property = typeof(ClassJobCategory).GetProperty(job.Abbreviation.ToString());
                    allowed = property?.GetValue(row.ClassJobCategory0.Value) is true;
                }
                categories[category] = allowed;
            }
            observations[id] = new(Unlocks.IsQuestCompleted(row), manager->IsQuestAccepted(id),
                Player.Level >= row.ClassJobLevel[0], allowed);
        }
        if (!Settings.Started.TryGetValue(CharacterId, out var started)) Settings.Started[CharacterId] = started = new();
        bool dirty = false;
        var instanceSheet = Data.GetExcelSheet<InstanceContent>();
        var results = new List<ActivityProgress>();
        foreach (var activity in Catalog.Activities)
        {
            if (!instanceSheet.TryGetRow(activity.InstanceId, out var row))
            {
                results.Add(new(activity, ActivityState.Unknown, null, false));
                continue;
            }
            // Accepted quests or progress inside the same Chronicles storyline, never a shared MSQ.
            if (ProgressResolver.HasChainProgress(activity, Catalog.Quests, observations))
                dirty |= started.Add(activity.Id);
            results.Add(ProgressResolver.Evaluate(activity, Unlocks.IsInstanceContentUnlocked(row),
                UIState.IsInstanceContentCompleted(activity.InstanceId), started.Contains(activity.Id), Catalog.Quests, observations));
        }
        Progress = results;
        Available = true;
        if (dirty) Save();
    }

    internal IEnumerable<ActivityProgress> Visible() => Progress.Where(p => !p.Activity.Advanced && !Hidden.Contains(p.Activity.Id));
    internal IEnumerable<ActivityProgress> Suggestions() => Visible()
        .Where(p => p.State is ActivityState.NotStarted or ActivityState.InProgress && p.Next is { Ready: true, Partial: false })
        .OrderByDescending(p => p.State == ActivityState.InProgress).ThenBy(p => p.Activity.Level)
        .DistinctBy(p => p.Next!.QuestId);
    internal string QuestName(uint id) => Catalog?.Quests.GetValueOrDefault(id)?.Name ?? $"Quest #{id}";

    public void Dispose()
    {
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= windows.Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/compass");
        windows.RemoveAllWindows();
    }
}
