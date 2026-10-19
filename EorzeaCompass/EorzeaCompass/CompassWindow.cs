using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EorzeaCompass;

internal sealed class CompassWindow : Window
{
    private readonly Plugin plugin;
    private string search = "";
    private int view;
    private int category;
    private uint expansion = uint.MaxValue;
    private uint selected;
    private bool readyOnly;
    private bool advanced;
    private bool hidden;
    private bool settings;
    private static readonly Vector4 Accent = new(.45f, .85f, .75f, 1);
    private static readonly Vector4 Gold = new(1, .78f, .42f, 1);
    private string T(string ru, string en) => plugin.T(ru, en);

    public CompassWindow(Plugin plugin) : base("Eorzea Compass###CompassMain")
    {
        this.plugin = plugin;
        Size = new(1100, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(760, 500), MaximumSize = new(1800, 1400) };
    }

    public override void Draw()
    {
        ImGui.TextColored(Accent, T("ЭОРЗЕЯ / КУДА ДАЛЬШЕ", "EORZEA / WHAT'S NEXT"));
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Настройки", "Settings"))) settings = !settings;
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Обновить", "Refresh"))) plugin.Refresh();
        if (settings) DrawSettings();
        if (!plugin.Available)
        {
            ImGui.TextWrapped(plugin.Error ?? T("Войди в игру. Прогресс появится через несколько секунд после загрузки персонажа.",
                "Log in. Progress will appear a few seconds after your character loads."));
            return;
        }
        ImGui.TextUnformatted(Plugin.Player.CharacterName);
        var visible = plugin.Visible().ToArray();
        var locked = visible.Count(p => p.State is ActivityState.NotStarted or ActivityState.InProgress);
        var unlocked = visible.Count(p => p.State is ActivityState.Unlocked or ActivityState.Cleared);
        ImGui.TextWrapped(T($"Ещё не открыто: {locked}   •   Открыто: {unlocked} / {visible.Length}",
            $"Still locked: {locked}   •   Unlocked: {unlocked} / {visible.Length}"));
        ImGui.TextDisabled(T("Счётчик обычного контента; прохождение = первый клир, а не все награды.",
            "Standard content count; cleared means a first clear, not all rewards."));
        ImGui.Separator();
        if (ImGui.CollapsingHeader(T("Можно заняться сейчас", "Things to do now"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            var suggestions = plugin.Suggestions().Take(3).ToArray();
            foreach (var p in suggestions)
                if (ImGui.Selectable($"{p.Activity.Name} → {plugin.QuestName(p.Next!.QuestId)}###suggest{p.Activity.Id}")) selected = p.Activity.Id;
            if (suggestions.Length == 0) ImGui.TextWrapped(T("Нет подтверждённых рекомендаций для текущего класса и уровня. Посмотри требования в каталоге.",
                "No confirmed recommendations for your current job and level. Check the catalog requirements."));
        }
        var labels = new[] { T("Ещё не открыто", "Still locked"), T("В процессе", "In progress"), T("Открыто", "Unlocked"), T("Весь каталог", "All content") };
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.RadioButton(labels[i], view == i)) view = i;
        }
        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##search", T("Название активности / квеста", "Activity / quest name"), ref search, 200);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(190);
        ImGui.Combo("##category", ref category, new[] { T("Все категории", "All categories"), T("Подземелья", "Dungeons"), T("Триалы", "Trials"), T("Рейды", "Raids"), T("Альянс-рейды", "Alliance raids") }, 5);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(190);
        var expansionName = expansion == uint.MaxValue ? T("Все дополнения", "All expansions")
            : plugin.Catalog!.Activities.First(a => a.Expansion == expansion).ExpansionName;
        if (ImGui.BeginCombo("##expansion", expansionName))
        {
            if (ImGui.Selectable(T("Все дополнения", "All expansions"), expansion == uint.MaxValue)) expansion = uint.MaxValue;
            foreach (var e in plugin.Catalog!.Activities.DistinctBy(a => a.Expansion))
                if (ImGui.Selectable(e.ExpansionName, expansion == e.Expansion)) expansion = e.Expansion;
            ImGui.EndCombo();
        }
        ImGui.Checkbox(T("Доступен следующий шаг", "Next step available"), ref readyOnly);
        ImGui.SameLine();
        ImGui.Checkbox(T("Сложные версии", "Advanced duties"), ref advanced);
        ImGui.SameLine();
        ImGui.Checkbox(T("Показать скрытое", "Show hidden"), ref hidden);
        var rows = plugin.Progress.Where(p => (advanced || !p.Activity.Advanced) && (hidden || !plugin.Hidden.Contains(p.Activity.Id)))
            .Where(p => view switch { 0 => p.State is ActivityState.NotStarted or ActivityState.InProgress,
                1 => p.State == ActivityState.InProgress, 2 => p.State is ActivityState.Unlocked or ActivityState.Cleared, _ => true })
            .Where(p => category == 0 || (int)p.Activity.Kind == category - 1)
            .Where(p => expansion == uint.MaxValue || p.Activity.Expansion == expansion)
            .Where(p => !readyOnly || p.Next is { Ready: true, Partial: false })
            .Where(p => search.Length == 0 || p.Activity.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.Activity.QuestIds.Any(q => plugin.QuestName(q).Contains(search, StringComparison.OrdinalIgnoreCase))
                || p.Next is { } step && plugin.QuestName(step.QuestId).Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        ImGui.Separator();
        if (ImGui.BeginChild("catalog", new Vector2(ImGui.GetContentRegionAvail().X * .48f, 0), true))
        {
            ImGui.TextDisabled(T($"Найдено: {rows.Length}", $"Matches: {rows.Length}"));
            foreach (var p in rows)
            {
                if (ImGui.Selectable($"{p.Activity.Name}###activity{p.Activity.Id}", selected == p.Activity.Id)) selected = p.Activity.Id;
                ImGui.TextDisabled($"{Kind(p.Activity.Kind)} • Lv. {p.Activity.Level} • {State(p.State)}");
            }
            if (rows.Length == 0) ImGui.TextWrapped(T("Нет активностей с такими фильтрами.", "No activities match these filters."));
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("detail", Vector2.Zero, true))
        {
            // Keep selected details visible when a duty becomes unlocked and leaves the locked list.
            var current = plugin.Progress.FirstOrDefault(p => p.Activity.Id == selected) ?? rows.FirstOrDefault();
            if (current != null) DrawDetail(current);
        }
        ImGui.EndChild();
    }

    private void DrawDetail(ActivityProgress p)
    {
        ImGui.TextWrapped(p.Activity.Name);
        ImGui.TextColored(Accent, State(p.State));
        ImGui.TextWrapped($"{p.Activity.ExpansionName} • {Kind(p.Activity.Kind)} • Lv. {p.Activity.Level} • iLv. {p.Activity.ItemLevel}");
        var isHidden = plugin.Hidden.Contains(p.Activity.Id);
        if (ImGui.SmallButton(isHidden ? T("Вернуть в рекомендации", "Restore to suggestions") : T("Скрыть активность", "Hide activity")))
            plugin.Hide(p.Activity.Id, !isHidden);
        ImGui.Separator();
        if (p.State == ActivityState.Cleared)
            ImGui.TextWrapped(T("Игра подтверждает первое прохождение этой активности. Завершение всей сюжетной линии и сбор наград здесь не оцениваются.",
                "The game confirms a first clear. This does not measure completion of the whole storyline or reward collection."));
        else if (p.State == ActivityState.Unlocked)
            ImGui.TextWrapped(T("Активность открыта. Первое прохождение ещё не подтверждено.", "Duty unlocked. First clear is not yet confirmed."));
        else if (p.State == ActivityState.Unknown)
            ImGui.TextWrapped(T("Данные этой активности пока недоступны.", "This duty's data is unavailable."));
        else if (p.Next is { } step)
        {
            ImGui.TextColored(Gold, T("Следующий шаг", "Next step"));
            ImGui.TextWrapped(Reason(step));
            if (step.QuestId != 0)
            {
                ImGui.TextWrapped(plugin.QuestName(step.QuestId));
                if (plugin.Catalog!.Quests.TryGetValue(step.QuestId, out var q))
                {
                    ImGui.TextUnformatted($"Lv. {q.Level} • Quest #{q.Id}");
                    if (q.Previous.Length > 0)
                    {
                        ImGui.TextWrapped(T(q.Join == 2 ? "Нужен один из квестов:" : "Предыдущие квесты:", q.Join == 2 ? "Requires one of:" : "Previous quests:"));
                        foreach (var id in q.Previous) ImGui.TextWrapped("• " + plugin.QuestName(id));
                    }
                }
                if (step.Accepted)
                    ImGui.TextWrapped(T("Продолжи задание по игровому журналу. Точка выдачи не является текущей целью.",
                        "Continue using your quest journal. The starting NPC is not your current objective."));
                else
                {
                    var location = QuestLocation.Read(step.QuestId);
                    if (location != null)
                    {
                        ImGui.TextWrapped($"{location.Npc}\n{location.Zone} (X: {location.X:F1}, Y: {location.Y:F1})");
                        if (ImGui.Button(T("Показать NPC на карте", "Show NPC on map"))) location.Open();
                    }
                    else ImGui.TextWrapped(T("Точная точка выдачи не подтверждена в игровых данных.", "The exact starting location is not confirmed by game data."));
                }
            }
            if (p.Activity.QuestIds.Length > 0)
            {
                ImGui.Separator();
                ImGui.TextWrapped(T("Квесты, связанные с открытием:", "Quests linked to the unlock:"));
                foreach (var id in p.Activity.QuestIds) ImGui.TextWrapped("• " + plugin.QuestName(id));
                ImGui.TextDisabled(T("Связь: ", "Source: ") + p.Activity.Source);
                if (p.Activity.Source == "Quest.INSTANCEDUNGEON")
                    ImGui.TextWrapped(T("Связь найдена по упоминанию активности в квесте; это ориентир для цепочки, а не гарантия момента открытия.",
                        "Linked through a quest script reference; this guides you to the chain but does not guarantee the exact unlock step."));
            }
            ImGui.Spacing();
            ImGui.TextWrapped(T("Прогресс цепочки определяется по принятым квестам и выполненным заданиям той же сюжетной линии Chronicles. История брошенных квестов до установки может отсутствовать.",
                "Chain progress uses accepted quests and completed prerequisites in the same Chronicles storyline. Pre-install abandonment history may be unavailable."));
        }
    }

    private string Reason(NextStep s) => s.Reason switch
    {
        "accepted" => T("Задание уже в журнале — можно продолжить.", "Quest already in your journal — continue it."),
        "offer" => T("Проверенные требования выполнены на текущем классе. Начни с этого квеста.", "Checked requirements are met on your current job. Start with this quest."),
        "level" => T("Нужен более высокий уровень текущего класса.", "Your current job needs a higher level."),
        "class" => T("Для этого задания нужен другой класс или профессия.", "This quest requires a different class or job."),
        "completed" => T("Квест выполнен, но активность ещё закрыта. Проверь дальнейшие условия в игре.", "Quest complete, but the duty is still locked. Check further requirements in game."),
        "unmapped" => T("Статус открытия известен, но надёжная связь с квестом пока не найдена.", "Unlock status is known, but no reliable quest link was found."),
        "ambiguous" => T("Найдено несколько возможных квестов. Точный маршрут требует проверки.", "Several possible quests found. The exact route needs verification."),
        _ => T("Есть дополнительные или неподтверждённые условия. Проверь требования в игре.", "Additional or unverified requirements exist. Check them in game."),
    };
    private string State(ActivityState s) => s switch
    {
        ActivityState.NotStarted => T("Ещё не открыто", "Still locked"),
        ActivityState.InProgress => T("В процессе", "In progress"),
        ActivityState.Unlocked => T("Открыто", "Unlocked"),
        ActivityState.Cleared => T("Пройдено", "Cleared"), _ => T("Неизвестно", "Unknown"),
    };
    private string Kind(ActivityKind k) => k switch
    {
        ActivityKind.Dungeon => T("Подземелье", "Dungeon"), ActivityKind.Trial => T("Триал", "Trial"),
        ActivityKind.AllianceRaid => T("Альянс-рейд", "Alliance raid"), _ => T("Рейд", "Raid"),
    };

    private void DrawSettings()
    {
        var ru = plugin.Settings.Russian;
        var notify = plugin.Settings.NotifyOnLogin;
        var open = plugin.Settings.OpenOnLogin;
        var dirty = ImGui.Checkbox("Русский интерфейс", ref ru);
        dirty |= ImGui.Checkbox(T("Подсказки в чате при входе", "Login suggestions in chat"), ref notify);
        dirty |= ImGui.Checkbox(T("Открывать окно при входе", "Open window on login"), ref open);
        if (dirty)
        {
            plugin.Settings.Russian = ru;
            plugin.Settings.NotifyOnLogin = notify;
            plugin.Settings.OpenOnLogin = open;
            plugin.Save();
        }
        ImGui.TextWrapped(T("Названия квестов и контента — на английском для поиска в игре. Прогресс и скрытые активности раздельны для каждого персонажа.",
            "Quest and duty names use English for in-game lookup. History and hidden duties are separate for each character."));
        ImGui.Separator();
    }
}
