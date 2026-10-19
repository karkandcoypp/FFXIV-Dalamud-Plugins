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
    private static readonly Vector4 Muted = new(.67f, .72f, .78f, 1);
    private string T(string ru, string en) => plugin.T(ru, en);
    private float S => ImGui.GetFontSize() / 17f;

    public CompassWindow(Plugin plugin) : base("Eorzea Compass###CompassMain")
    {
        this.plugin = plugin;
        Size = new(1180, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(960, 600), MaximumSize = new(2200, 1600) };
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowBgAlpha(1);
        SizeConstraints = new() { MinimumSize = new(960 * S, 600 * S), MaximumSize = new(2200 * S, 1600 * S) };
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * S);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7) * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7 * S);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.085f, .10f, .12f, 1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.16f, .24f, .28f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.21f, .37f, .40f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.16f, .32f, .34f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.20f, .39f, .41f, 1));
        try { DrawContent(); }
        finally { ImGui.PopStyleColor(5); ImGui.PopStyleVar(3); }
    }

    private void DrawContent()
    {
        ImGui.TextColored(Accent, T("Твой путеводитель по Эорзее", "Your guide to Eorzea"));
        if (plugin.Available)
            ImGui.TextColored(Muted, $"{Plugin.Player.CharacterName}   /   {Plugin.Player.ClassJob.Value.Abbreviation} · {T("ур.", "Lv.")} {Plugin.Player.Level}");
        ImGui.Separator();
        if (!plugin.Available)
        {
            ImGui.TextWrapped(plugin.Error ?? T("Ожидание персонажа. После входа каталог загрузится автоматически.", "Waiting for your character. The catalog loads automatically after login."));
            return;
        }
        if (ImGui.BeginChild("navigation", new Vector2(185 * S, 0), true)) DrawNavigation();
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("workspace", Vector2.Zero, false))
        {
            if (settings) DrawSettings(); else DrawCatalog();
        }
        ImGui.EndChild();
    }

    private string ViewName(int index) => index switch
    {
        0 => T("Ещё не открыто", "Still locked"), 1 => T("Продолжить", "Continue"),
        2 => T("Уже открыто", "Unlocked"), _ => T("Весь контент", "All content"),
    };

    private void DrawNavigation()
    {
        var normal = plugin.Visible().ToArray();
        var unlocked = normal.Count(p => p.State is ActivityState.Unlocked or ActivityState.Cleared);
        ImGui.TextColored(Muted, T("ОТКРЫТИЕ КОНТЕНТА", "CONTENT UNLOCKS"));
        ImGui.TextUnformatted($"{unlocked} / {normal.Length}");
        ImGui.ProgressBar(normal.Length == 0 ? 0 : (float)unlocked / normal.Length, new Vector2(-1, 7 * S), "");
        ImGui.Spacing();
        for (int i = 0; i < 4; i++)
            if (ImGui.Selectable($"{ViewName(i)}  ({normal.Count(p => MatchesView(p, i))})###view{i}", !settings && view == i,
                ImGuiSelectableFlags.None, new Vector2(0, 32 * S))) { view = i; settings = false; }
        ImGui.Separator();
        ImGui.TextColored(Muted, T("КАТЕГОРИИ", "CATEGORIES"));
        var names = new[] { T("Все активности", "All activities"), T("Подземелья", "Dungeons"), T("Триалы", "Trials"), T("Рейды", "Raids"), T("Альянс-рейды", "Alliance raids") };
        for (int i = 0; i < names.Length; i++)
            if (ImGui.Selectable(names[i], category == i, ImGuiSelectableFlags.None, new Vector2(0, 26 * S))) { category = i; settings = false; }
        ImGui.Separator();
        if (ImGui.Selectable(T("Настройки", "Settings"), settings, ImGuiSelectableFlags.None, new Vector2(0, 28 * S))) settings = !settings;
        if (ImGui.Button(T("Обновить прогресс", "Refresh progress"), new Vector2(-1, 0))) plugin.Refresh();
        ImGui.Spacing();
        ImGui.TextWrapped(T("Счётчики: обычный контент", "Counts: standard duties"));
    }

    private static bool MatchesView(ActivityProgress p, int index) => index switch
    {
        0 => p.State is ActivityState.NotStarted or ActivityState.InProgress,
        1 => p.State == ActivityState.InProgress,
        2 => p.State is ActivityState.Unlocked or ActivityState.Cleared, _ => true,
    };

    private void DrawCatalog()
    {
        ImGui.TextColored(Accent, ViewName(view));
        ImGui.SetNextItemWidth(Math.Max(160 * S, ImGui.GetContentRegionAvail().X * .52f));
        ImGui.InputTextWithHint("##search", T("Поиск по активности или квесту…", "Search activities or quests…"), ref search, 200);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var expansionName = expansion == uint.MaxValue ? T("Все дополнения", "All expansions")
            : plugin.Catalog!.Activities.First(a => a.Expansion == expansion).ExpansionName;
        if (ImGui.BeginCombo("##expansion", expansionName))
        {
            if (ImGui.Selectable(T("Все дополнения", "All expansions"), expansion == uint.MaxValue)) expansion = uint.MaxValue;
            foreach (var e in plugin.Catalog!.Activities.DistinctBy(a => a.Expansion))
                if (ImGui.Selectable(e.ExpansionName, expansion == e.Expansion)) expansion = e.Expansion;
            ImGui.EndCombo();
        }
        ImGui.Checkbox(T("Можно начать сейчас", "Ready now"), ref readyOnly);
        ImGui.SameLine();
        if (ImGui.Button(T("Ещё фильтры", "More filters"))) ImGui.OpenPopup("filters");
        if (ImGui.BeginPopup("filters"))
        {
            ImGui.Checkbox(T("Сложные версии", "Advanced duties"), ref advanced);
            ImGui.Checkbox(T("Показать скрытые", "Show hidden"), ref hidden);
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Сбросить", "Reset"))) { search = ""; category = 0; expansion = uint.MaxValue; readyOnly = advanced = hidden = false; }
        var rows = plugin.Progress.Where(p => (advanced || !p.Activity.Advanced) && (hidden || !plugin.Hidden.Contains(p.Activity.Id)))
            .Where(p => MatchesView(p, view)).Where(p => category == 0 || (int)p.Activity.Kind == category - 1)
            .Where(p => expansion == uint.MaxValue || p.Activity.Expansion == expansion)
            .Where(p => !readyOnly || p.Next is { Ready: true, Partial: false })
            .Where(p => search.Length == 0 || p.Activity.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.Activity.QuestIds.Any(q => plugin.QuestName(q).Contains(search, StringComparison.OrdinalIgnoreCase))
                || p.Next is { } step && plugin.QuestName(step.QuestId).Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Next is { Ready: true, Partial: false }).ThenByDescending(p => p.State == ActivityState.InProgress)
            .ThenBy(p => p.Activity.Expansion).ThenBy(p => p.Activity.Level).ToArray();
        selected = CompassSelection.KeepVisible(selected, rows);
        ImGui.TextColored(Muted, T($"Активностей: {rows.Length}", $"Activities: {rows.Length}"));
        ImGui.Separator();
        var width = Math.Clamp(ImGui.GetContentRegionAvail().X * .42f, 240 * S, 390 * S);
        if (ImGui.BeginChild("activity-list", new Vector2(width, 0), true))
        {
            foreach (var p in rows) DrawRow(p);
            if (rows.Length == 0)
            {
                ImGui.TextWrapped(T("Здесь пока ничего нет.", "Nothing here right now."));
                ImGui.TextWrapped(readyOnly ? T("На текущем классе нет подтверждённых шагов. Отключи фильтр «Можно начать сейчас», чтобы увидеть требования.", "No confirmed steps for your current job. Turn off Ready now to see requirements.")
                    : T("Попробуй сбросить фильтры или выбрать «Весь контент».", "Reset filters or choose All content."));
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("activity-detail", Vector2.Zero, true))
        {
            var current = rows.FirstOrDefault(p => p.Activity.Id == selected);
            if (current != null) DrawDetail(current);
            else ImGui.TextWrapped(T("Выбери активность в списке слева.", "Choose an activity from the list."));
        }
        ImGui.EndChild();
    }

    private void DrawRow(ActivityProgress p)
    {
        ImGui.PushID((int)p.Activity.Id);
        var start = ImGui.GetCursorPos();
        var available = ImGui.GetContentRegionAvail().X;
        var titleHeight = ImGui.CalcTextSize(p.Activity.Name, false, available - 16 * S).Y;
        var height = Math.Max(94 * S, titleHeight + ImGui.GetTextLineHeight() * 2 + 36 * S);
        if (ImGui.Selectable("##row", selected == p.Activity.Id, ImGuiSelectableFlags.None, new Vector2(0, height))) selected = p.Activity.Id;
        var after = ImGui.GetCursorPos();
        ImGui.SetCursorPos(start + new Vector2(8, 5) * S);
        ImGui.PushTextWrapPos(start.X + available - 8 * S);
        ImGui.TextUnformatted(p.Activity.Name);
        ImGui.TextColored(Muted, $"{Kind(p.Activity.Kind)} · {T("ур.", "Lv.")} {p.Activity.Level}");
        ImGui.TextColored(Color(p), ShortStatus(p));
        ImGui.PopTextWrapPos();
        ImGui.SetCursorPos(after);
        ImGui.Separator();
        ImGui.PopID();
    }

    private Vector4 Color(ActivityProgress p) => p.Next is { Ready: true } || p.State == ActivityState.Cleared ? Accent : p.State == ActivityState.InProgress ? Gold : Muted;
    private string ShortStatus(ActivityProgress p) => p.Next?.Reason switch
    {
        "accepted" => T("Квест уже в журнале", "Quest in your journal"), "offer" => T("Можно взять квест", "Quest ready to accept"),
        "class" => T("Нужен другой класс", "Switch class or job"), "level" => T("Нужен уровень выше", "Higher level needed"), _ => State(p.State),
    };

    private void DrawDetail(ActivityProgress p)
    {
        ImGui.TextColored(Accent, Kind(p.Activity.Kind));
        ImGui.TextWrapped(p.Activity.Name);
        ImGui.TextColored(Muted, $"{p.Activity.ExpansionName} · {T("ур.", "Lv.")} {p.Activity.Level} · iLv {p.Activity.ItemLevel}");
        ImGui.Spacing();
        ImGui.TextColored(Color(p), ShortStatus(p));
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(Gold, T("ЧТО СДЕЛАТЬ ДАЛЬШЕ", "WHAT TO DO NEXT"));
        if (p.State is ActivityState.Cleared or ActivityState.Unlocked)
            ImGui.TextWrapped(p.State == ActivityState.Cleared
                ? T("Активность открыта и пройдена. Можно повторить её через Duty Finder.", "Unlocked and cleared. You can replay it through Duty Finder.")
                : T("Открой Duty Finder и выбери эту активность для первого прохождения.", "Open Duty Finder and select this duty for your first clear."));
        else if (p.Next is { } step)
        {
            ImGui.TextWrapped(Reason(step));
            if (step.QuestId != 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(Muted, T("КВЕСТ", "QUEST"));
                ImGui.TextWrapped(plugin.QuestName(step.QuestId));
                if (plugin.Catalog!.Quests.TryGetValue(step.QuestId, out var q))
                {
                    ImGui.TextColored(Muted, T($"Требуется уровень {q.Level}", $"Required level: {q.Level}"));
                    if (step.Reason == "class") ImGui.TextWrapped(plugin.EligibleJobs(q));
                }
                if (step.Accepted)
                    ImGui.TextWrapped(T("Открой журнал заданий (J) и продолжи этот квест по текущей цели.", "Open your quest journal (J) and follow this quest's current objective."));
                else if (step.Reason != "completed")
                {
                    var location = QuestLocation.Read(step.QuestId);
                    if (location != null)
                    {
                        ImGui.Spacing();
                        ImGui.TextColored(Muted, T("ГДЕ ВЗЯТЬ", "WHERE TO START"));
                        ImGui.TextWrapped(location.Npc);
                        ImGui.TextWrapped($"{location.Zone}\nX: {location.X:F1}   Y: {location.Y:F1}");
                        ImGui.Spacing();
                        if (ImGui.Button(T("Показать на карте", "Show on map"), new Vector2(-1, 40 * S))) location.Open();
                    }
                    else ImGui.TextWrapped(T("Точка выдачи пока не определена.", "Starting location is not yet available."));
                }
            }
        }
        else ImGui.TextWrapped(T("Прогресс пока недоступен. Нажми «Обновить прогресс».", "Progress is unavailable. Click Refresh progress."));
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.CollapsingHeader(T("Требования и связанные квесты", "Requirements and linked quests")))
        {
            var id = p.Next?.QuestId ?? 0;
            if (plugin.Catalog!.Quests.TryGetValue(id, out var q))
            {
                ImGui.TextWrapped(T(q.Join == 2 ? "Достаточно одного предыдущего квеста:" : "Предыдущие квесты:", q.Join == 2 ? "Complete one prerequisite:" : "Previous quests:"));
                foreach (var previous in q.Previous)
                {
                    var done = plugin.QuestProgress.TryGetValue(previous, out var observation) && observation.Complete;
                    ImGui.TextWrapped($"{(done ? "[+]" : "[ ]")} {plugin.QuestName(previous)}");
                }
                if (q.Previous.Length == 0) ImGui.TextWrapped(T("Предыдущие квесты не указаны.", "No previous quests listed."));
            }
            foreach (var quest in p.Activity.QuestIds) ImGui.TextWrapped("• " + plugin.QuestName(quest));
        }
        if (ImGui.CollapsingHeader(T("Как определяется прогресс", "How progress is tracked")))
        {
            ImGui.TextWrapped(T("«Пройдено» означает первый клир активности, а не сбор всех наград. История отменённых до установки квестов может отсутствовать.", "Cleared means a first clear, not all rewards. Quests abandoned before installation may have no history."));
            if (p.Activity.Source == "Quest.INSTANCEDUNGEON")
                ImGui.TextWrapped(T("Связь с квестом найдена в сценарии игры. Точный момент открытия может отличаться.", "The quest link comes from a game script. The exact unlock moment may differ."));
            ImGui.TextDisabled($"Duty #{p.Activity.Id} · {p.Activity.Source}");
        }
        ImGui.Spacing();
        var isHidden = plugin.Hidden.Contains(p.Activity.Id);
        if (ImGui.SmallButton(isHidden ? T("Вернуть в список", "Restore activity") : T("Скрыть эту активность", "Hide this activity"))) plugin.Hide(p.Activity.Id, !isHidden);
    }

    private string Reason(NextStep s) => s.Reason switch
    {
        "accepted" => T("Продолжи принятое задание.", "Continue the quest you have accepted."),
        "offer" => T("Поговори с NPC и возьми задание.", "Speak to the NPC and accept the quest."),
        "level" => T("Повысь уровень текущего класса или переключись на подходящую профессию.", "Level up your current class or switch to an eligible job."),
        "class" => T("Переключись на подходящий класс, затем возьми задание у NPC.", "Switch to an eligible class or job, then accept the quest from the NPC."),
        "completed" => T("Квест выполнен, но активность ещё закрыта. Проверь дополнительные условия в игре.", "Quest complete, but the duty is locked. Check further requirements in game."),
        "unmapped" => T("Активность ещё закрыта. Маршрут открытия пока не добавлен.", "This duty is locked. Its unlock route is not available yet."),
        "ambiguous" => T("Есть несколько вариантов открытия. Посмотри связанные квесты ниже.", "Several unlock routes were found. See the linked quests below."),
        _ => T("Сначала проверь дополнительные условия этого задания в игре.", "Check this quest's additional requirements in game first."),
    };
    private string State(ActivityState s) => s switch
    {
        ActivityState.NotStarted => T("Не открыто", "Locked"), ActivityState.InProgress => T("Цепочка начата", "Chain started"),
        ActivityState.Unlocked => T("Открыто · ещё не пройдено", "Unlocked · not yet cleared"),
        ActivityState.Cleared => T("Открыто и пройдено", "Unlocked and cleared"), _ => T("Прогресс неизвестен", "Progress unknown"),
    };
    private string Kind(ActivityKind k) => k switch
    {
        ActivityKind.Dungeon => T("Подземелье", "Dungeon"), ActivityKind.Trial => T("Триал", "Trial"),
        ActivityKind.AllianceRaid => T("Альянс-рейд", "Alliance raid"), _ => T("Рейд", "Raid"),
    };

    private void DrawSettings()
    {
        ImGui.TextColored(Accent, T("Настройки", "Settings"));
        ImGui.Spacing();
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
        ImGui.Spacing();
        ImGui.TextWrapped(T("Названия квестов и активностей оставлены на английском для поиска в игре. Прогресс и скрытые активности сохраняются отдельно для каждого персонажа.", "Quest and duty names use English for in-game lookup. Progress and hidden activities are saved separately for each character."));
        ImGui.Spacing();
        if (ImGui.Button(T("Вернуться к активностям", "Back to activities"))) settings = false;
    }
}
