using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;

namespace EorzeaCompass;

internal sealed partial class CompassWindow : Window
{
    private readonly Plugin plugin;
    private readonly Func<uint, QuestLocation?> locate;
    private string search = "";
    private int view;
    private int category;
    private uint expansion = uint.MaxValue;
    private uint selected;
    private bool readyOnly;
    private bool advanced;
    private bool hidden;
    private bool settings;
    private int section;
    private int storyFilter;
    private int storyKind;
    private string selectedStory = "";
    private string atlasStory = "";
    private int rewardFilter;
    private int selectedReward = -1;
    private int rewardCategory;
    private static readonly Vector4 Accent = new(.45f, .85f, .75f, 1);
    private static readonly Vector4 Gold = new(1, .78f, .42f, 1);
    private static readonly Vector4 Muted = new(.67f, .72f, .78f, 1);
    private string T(string ru, string en) => plugin.T(ru, en);
    private float S => ImGui.GetFontSize() / 17f;

    public CompassWindow(Plugin plugin, Func<uint, QuestLocation?>? locate = null) : base("Eorzea Compass###CompassMain")
    {
        this.plugin = plugin;
        this.locate = locate ?? QuestLocation.Read;
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
        CompassTheme.Push(S);
        try { DrawContent(); }
        finally { CompassTheme.Pop(); }
    }

    private void DrawContent()
    {
        ImGui.TextColored(Accent, T("Твой журнал прогресса", "Your progress journal"));
        if (plugin.Available)
        {
            ImGui.SameLine(0, 24 * S);
            ImGui.TextColored(Muted, $"{Plugin.Player.CharacterName}   /   {Plugin.Player.ClassJob.Value.Abbreviation} · {T("ур.", "Lv.")} {Plugin.Player.Level}");
        }
        ImGui.Separator();
        if (!plugin.Available)
        {
            ImGui.TextWrapped(plugin.Error ?? T("Ожидание персонажа. После входа каталог загрузится автоматически.", "Waiting for your character. The catalog loads automatically after login."));
            return;
        }
        if (ImGui.BeginChild("navigation", new Vector2(198 * S, 0), true)) DrawNavigation();
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("workspace", Vector2.Zero, false))
        {
            if (settings) DrawSettings();
            else if (section == 0) DrawOverview();
            else if (section == 1) DrawStories();
            else if (section == 2) DrawCatalog();
            else DrawAtlas();
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
        ImGui.TextColored(Accent, T("ЖУРНАЛ ПРОГРЕССА", "PROGRESS JOURNAL"));
        var labels = new[] { T("Обзор дополнений", "Expansion overview"), T("Цепочки заданий", "Quest chains"), T("Активности", "Activities"), T("Атлас наград", "Reward atlas") };
        for (int i = 0; i < labels.Length; i++)
            if (ImGui.Selectable(labels[i], !settings && section == i, ImGuiSelectableFlags.None, new Vector2(0, 28 * S)))
            { section = i; settings = false; if (i == 3) atlasStory = ""; }
        ImGui.Separator();
        ImGui.TextColored(Muted, T("ДОПОЛНЕНИЕ", "EXPANSION"));
        if (ImGui.Selectable(T("Все дополнения", "All expansions"), expansion == uint.MaxValue)) expansion = uint.MaxValue;
        foreach (var e in plugin.Expansions)
            if (ImGui.Selectable(e.Name, expansion == e.Id, ImGuiSelectableFlags.None, new Vector2(0, 24 * S))) expansion = e.Id;
        if (section == 2 && !settings)
        {
            ImGui.Separator();
            ImGui.TextColored(Muted, T("КАТЕГОРИЯ", "CATEGORY"));
            var kinds = new[] { T("Все активности", "All activities"), T("Подземелья", "Dungeons"), T("Триалы", "Trials"), T("Рейды", "Raids"), T("Альянс-рейды", "Alliance raids") };
            for (int i = 0; i < kinds.Length; i++)
                if (ImGui.Selectable(kinds[i], category == i)) category = i;
        }
        ImGui.Separator();
        if (ImGui.Selectable(T("Настройки", "Settings"), settings)) settings = !settings;
        if (ImGui.Button(T("Обновить прогресс", "Refresh progress"), new Vector2(-1, 0))) plugin.Refresh();
    }

    private void DrawOverview()
    {
        ImGui.TextColored(Accent, T("Твоя история в Эорзее", "Your journey through Eorzea"));
        ImGui.TextWrapped(T("Сюжетные истории и прохождение активностей учитываются отдельно.", "Story completion and duty progress are tracked separately."));
        ImGui.Separator();
        if (ImGui.BeginChild("overview-scroll", Vector2.Zero, false))
        {
            var expansions = plugin.Expansions.Where(e => expansion == uint.MaxValue || expansion == e.Id).ToArray();
            if (expansions.Length == 0) ImGui.TextWrapped(T("Нет дополнений для выбранного фильтра.", "No expansions match this filter."));
            int columns = ImGui.GetContentRegionAvail().X >= 680 * S ? 2 : 1;
            var width = (ImGui.GetContentRegionAvail().X - (columns - 1) * 8 * S) / columns;
            for (int i = 0; i < expansions.Length; i++)
            {
                if (i % columns != 0) ImGui.SameLine();
                DrawExpansion(expansions[i], width);
            }
        }
        ImGui.EndChild();
    }

    private void DrawExpansion(ExpansionProgress e, float width)
    {
        if (ImGui.BeginChild("expansion-" + e.Id, new Vector2(width, 164 * S), true))
        {
            ImGui.TextColored(Accent, e.Name);
            ImGui.Separator();
            ImGui.TextUnformatted(T($"Истории каталога: {e.StoriesDone} из {e.StoriesTotal}", $"Catalog stories: {e.StoriesDone} of {e.StoriesTotal}"));
            ImGui.ProgressBar(e.StoriesTotal == 0 ? 0 : (float)e.StoriesDone / e.StoriesTotal, new Vector2(-1, 5 * S), "");
            ImGui.TextUnformatted(T($"Активности открыты: {e.ActivitiesUnlocked} из {e.ActivitiesTotal}", $"Duties unlocked: {e.ActivitiesUnlocked} of {e.ActivitiesTotal}"));
            ImGui.TextUnformatted(T($"Первое прохождение: {e.ActivitiesCleared} из {e.ActivitiesTotal}", $"First clears: {e.ActivitiesCleared} of {e.ActivitiesTotal}"));
            if (e.StoriesUnknown + e.ActivitiesUnknown > 0)
                ImGui.TextColored(Gold, T($"Нет данных: {e.StoriesUnknown + e.ActivitiesUnknown}", $"Unknown: {e.StoriesUnknown + e.ActivitiesUnknown}"));
            if (ImGui.Button(T("Цепочки", "Storylines") + "##" + e.Id)) { expansion = e.Id; section = 1; search = ""; storyFilter = 0; }
            ImGui.SameLine();
            if (ImGui.Button(T("Активности", "Activities") + "##" + e.Id)) { expansion = e.Id; section = 2; search = ""; view = 3; }
        }
        ImGui.EndChild();
    }

    private void DrawStories()
    {
        ImGui.TextColored(Accent, T("Цепочки заданий", "Quest chains"));
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##story-search", T("Поиск истории или квеста…", "Search a story or quest…"), ref search, 200);
        ImGui.SetNextItemWidth(180 * S);
        ImGui.Combo("##story-state", ref storyFilter, new[] { T("Все состояния", "All states"), T("Не начато", "Not started"), T("В процессе", "In progress"), T("Завершено", "Completed") }, 4);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180 * S);
        ImGui.Combo("##story-kind", ref storyKind, new[] { T("Все цепочки", "All chains"), T("Основной сюжет", "Main scenario"), T("Рейды и триалы", "Chronicles"), T("Побочные истории", "Side stories"), T("Классы и профессии", "Classes and jobs"), T("Общества", "Societies"), T("Реликты", "Relics"), T("Исследование", "Exploration") }, 8);
        var kinds = new[] { "", "Main", "Chronicles", "Side", "Jobs", "Society", "Relic", "Exploration" };
        var rows = plugin.Stories.Where(p => expansion == uint.MaxValue || p.Story.Definition.Expansion == expansion)
            .Where(p => storyFilter == 0 || (int)p.State == storyFilter)
            .Where(p => storyKind == 0 || p.Story.Definition.Kind == kinds[storyKind])
            .Where(p => search.Length == 0 || p.Story.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.Route.Any(id => plugin.QuestName(id).Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Story.Definition.Expansion).ThenBy(p => p.Story.Definition.Kind == "Main" ? 0 : p.Story.Definition.Kind == "Chronicles" ? 1 : 2).ThenBy(p => p.Story.Definition.Id).ToArray();
        if (!rows.Any(p => p.Story.Definition.Id == selectedStory)) selectedStory = rows.FirstOrDefault()?.Story.Definition.Id ?? "";
        ImGui.TextColored(Muted, T($"Историй: {rows.Length}", $"Stories: {rows.Length}"));
        ImGui.Separator();
        if (ImGui.BeginChild("story-list", new Vector2(ImGui.GetContentRegionAvail().X * .44f, 0), true))
        {
            uint lastExpansion = uint.MaxValue;
            foreach (var row in rows)
            {
                if (lastExpansion != row.Story.Definition.Expansion)
                {
                    lastExpansion = row.Story.Definition.Expansion;
                    ImGui.TextColored(Muted, plugin.Expansions.FirstOrDefault(e => e.Id == lastExpansion)?.Name ?? "");
                    ImGui.Separator();
                }
                DrawStoryRow(row);
            }
            if (rows.Length == 0) ImGui.TextWrapped(T("Историй с такими фильтрами нет. Выбери «Все состояния» или другое дополнение.", "No matching stories. Choose All states or a different expansion."));
        }
        ImGui.EndChild(); ImGui.SameLine();
        if (ImGui.BeginChild("story-detail", Vector2.Zero, true))
        {
            var current = rows.FirstOrDefault(p => p.Story.Definition.Id == selectedStory);
            if (current != null) DrawStoryDetail(current);
            else ImGui.TextWrapped(T("Выбери историю в списке.", "Choose a story from the list."));
        }
        ImGui.EndChild();
    }

    private Vector4 StoryColor(StoryProgress p) => p.State == StoryState.Completed ? Accent : p.State == StoryState.InProgress ? Gold : Muted;
    private string StoryStatus(StoryProgress p) => p.State switch
    {
        StoryState.Completed => p.Story.Definition.CurrentChapter ? T("Пройден текущий этап", "Current chapter completed")
            : p.Story.Definition.Kind is "Relic" or "Jobs" ? T("Этапы цепочки пройдены", "Chain milestones completed") : T("История завершена", "Story completed"),
        StoryState.InProgress => T("В процессе", "In progress"), StoryState.NotStarted => T("Не начато", "Not started"), _ => T("Нет данных", "Unknown"),
    };
    private void DrawStoryRow(StoryProgress p)
    {
        ImGui.PushID(p.Story.Definition.Id);
        var start = ImGui.GetCursorPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleHeight = ImGui.CalcTextSize(p.Story.Definition.Name, false, width - 16 * S).Y;
        var height = titleHeight + ImGui.GetTextLineHeight() + 15 * S;
        if (ImGui.Selectable("##story", selectedStory == p.Story.Definition.Id, ImGuiSelectableFlags.None, new Vector2(0, height))) selectedStory = p.Story.Definition.Id;
        var after = ImGui.GetCursorPos(); ImGui.SetCursorPos(start + new Vector2(8, 3) * S);
        ImGui.PushTextWrapPos(start.X + width - 8 * S);
        ImGui.TextUnformatted(p.Story.Definition.Name);
        ImGui.TextColored(StoryColor(p), StoryStatus(p));
        ImGui.PopTextWrapPos(); ImGui.SetCursorPos(after); ImGui.Separator();
        ImGui.PopID();
    }

    private void DrawStoryDetail(StoryProgress p)
    {
        ImGui.TextWrapped(p.Story.Definition.Name);
        ImGui.TextColored(StoryColor(p), StoryStatus(p));
        var coverage = p.Story.Definition.Kind switch
        {
            "Jobs" => T("Квесты выбранного дополнения", "Quests for this expansion"),
            "Society" => T("История общества · репутация отдельно", "Society story · reputation separate"),
            "Relic" => T("Однократные этапы · не коллекция оружия", "One-time milestones · not a weapon collection"),
            "Exploration" => T("Сюжет зоны · не все этажи и награды", "Zone story · not all floors or rewards"),
            _ => "",
        };
        if (coverage.Length > 0) ImGui.TextWrapped(coverage);
        if (p.State != StoryState.Unknown && p.Story.Definition.Achievement == 0)
        {
            ImGui.TextUnformatted(T($"Задания маршрута: {p.Done} из {p.Route.Length}", $"Route quests: {p.Done} of {p.Route.Length}"));
            ImGui.ProgressBar(p.Fraction, new Vector2(-1, 5 * S), "");
        }
        ImGui.Separator();
        if (p.State == StoryState.Completed)
            ImGui.TextWrapped(T("Условие завершения подтверждено. Указанный этап пройден.", "Completion confirmed. The indicated chapter is finished."));
        else if (p.Next != null)
        {
            ImGui.TextColored(Gold, T("СЛЕДУЮЩИЙ ШАГ", "NEXT STEP"));
            DrawQuestAction(p.Next, p.State == StoryState.InProgress);
        }
        else if (p.Story.Definition.Achievement != 0)
            ImGui.TextWrapped(p.State == StoryState.Unknown
                ? T("Открой окно достижений в игре, затем обнови прогресс.", "Open the in-game Achievements window, then refresh progress.")
                : T("Продолжи сюжет внутри зоны. Завершение подтвердит достижение:", "Continue the scenario inside the zone. Completion requires this achievement:"));
        else ImGui.TextWrapped(T("Данных недостаточно для подтверждения прогресса. Нажми «Обновить прогресс».", "Not enough data to confirm progress. Click Refresh progress."));
        if (p.Story.Definition.Achievement != 0) ImGui.TextWrapped(p.Story.Definition.AchievementName);
        var rewards = plugin.Catalog!.Rewards.Count(r => r.StoryIds.Contains(p.Story.Definition.Id));
        if (rewards > 0 && ImGui.Button(T($"Награды цепочки ({rewards})", $"Chain rewards ({rewards})"), new Vector2(-1, 0)))
        { atlasStory = p.Story.Definition.Id; section = 3; search = ""; rewardFilter = rewardCategory = 0; selectedReward = -1; atlasMode = 1; atlasTabRequest = true; ownedFilter = 0; atlasSource = ""; }
        if (p.Story.Definition.CurrentChapter)
            ImGui.TextWrapped(T("Считаются задания до текущей точки каталога; история может продолжиться в новых обновлениях.", "Tracks the catalog's current endpoint; future patches may continue this story."));
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.11f, .13f, .15f, 1));
        if (ImGui.CollapsingHeader(T("Задания этой истории", "Story quests")))
            foreach (var id in p.Route)
            {
                var o = plugin.QuestProgress.GetValueOrDefault(id);
                ImGui.TextWrapped($"{(o?.Complete == true ? "[+]" : o?.Accepted == true ? "[~]" : "[ ]")} {plugin.QuestName(id)}");
            }
        if (ImGui.CollapsingHeader(T("Связанные активности", "Related activities")))
        {
            var duties = plugin.Progress.Where(a => p.Story.ActivityIds.Contains(a.Activity.Id)).ToArray();
            foreach (var a in duties)
                if (ImGui.Selectable(a.Activity.Name + " · " + State(a.State)))
                {
                    section = 2; expansion = a.Activity.Expansion; view = 3; selected = a.Activity.Id;
                    category = 0; search = ""; readyOnly = false; hidden = plugin.Hidden.Contains(a.Activity.Id);
                }
            if (duties.Length == 0) ImGui.TextWrapped(T("В каталоге нет связанных боёв.", "No linked duties in the catalog."));
        }
        if (ImGui.CollapsingHeader(T("Условие завершения", "Completion rule")))
        {
            ImGui.TextWrapped(p.Story.Definition.Achievement == 0 ? T("Выполнить финальный квест:", "Complete the final quest:") : T("Получить достижение:", "Earn this achievement:"));
            ImGui.TextWrapped(p.Story.Definition.Achievement == 0 ? p.Story.Definition.EndQuestName : p.Story.Definition.AchievementName);
            if (p.Story.Definition.Scope.Length > 0) ImGui.TextWrapped(T(p.Story.Definition.ScopeRu, p.Story.Definition.Scope));
            ImGui.TextWrapped(T("Счётчик охватывает маршрут к этому квесту. Дополнительные ветки и награды не входят в процент истории.", "The count covers the route to this quest. Optional branches and rewards are not part of story completion."));
        }
        ImGui.PopStyleColor();
    }

    private static bool MatchesView(ActivityProgress p, int index) => index switch
    {
        0 => p.State is ActivityState.NotStarted or ActivityState.InProgress,
        1 => p.State == ActivityState.InProgress,
        2 => p.State is ActivityState.Unlocked or ActivityState.Cleared, _ => true,
    };

    private void DrawCatalog()
    {
        ImGui.TextColored(Accent, T("Активности", "Activities"));
        ImGui.SetNextItemWidth(190 * S);
        ImGui.Combo("##activity-state", ref view, new[] { ViewName(0), ViewName(1), ViewName(2), ViewName(3) }, 4);
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
        var height = Math.Max(58 * S, titleHeight + ImGui.GetTextLineHeight() * 2 + 12 * S);
        if (ImGui.Selectable("##row", selected == p.Activity.Id, ImGuiSelectableFlags.None, new Vector2(0, height))) selected = p.Activity.Id;
        var after = ImGui.GetCursorPos();
        ImGui.SetCursorPos(start + new Vector2(8, 4) * S);
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
        "accepted" => T("Задание в журнале", "Quest in journal"),
        "offer" when p.State == ActivityState.InProgress => T("Следующий квест доступен", "Next quest ready"),
        "offer" => T("Можно начать цепочку", "Ready to start"),
        "class" => T("Нужен другой класс", "Switch class or job"), "level" => T("Нужен уровень выше", "Higher level needed"), _ => State(p.State),
    };

    private void DrawDetail(ActivityProgress p)
    {
        ImGui.TextWrapped(p.Activity.Name);
        ImGui.TextWrapped($"{Kind(p.Activity.Kind)} · {p.Activity.ExpansionName} · {T("ур.", "Lv.")} {p.Activity.Level} · iLv {p.Activity.ItemLevel}");
        ImGui.TextColored(Color(p), State(p.State));
        ImGui.Separator();
        ImGui.TextColored(Gold, T("ЧТО СДЕЛАТЬ ДАЛЬШЕ", "WHAT TO DO NEXT"));
        if (p.State is ActivityState.Cleared or ActivityState.Unlocked)
            ImGui.TextWrapped(p.State == ActivityState.Cleared
                ? T("Активность открыта и пройдена. Можно повторить её через Duty Finder.", "Unlocked and cleared. You can replay it through Duty Finder.")
                : T("Открой Duty Finder и выбери эту активность для первого прохождения.", "Open Duty Finder and select this duty for your first clear."));
        else if (p.Next is { } step) DrawQuestAction(step, p.State == ActivityState.InProgress);
        else ImGui.TextWrapped(T("Прогресс пока недоступен. Нажми «Обновить прогресс».", "Progress is unavailable. Click Refresh progress."));
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.11f, .13f, .15f, 1));
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
        ImGui.PopStyleColor();
        var isHidden = plugin.Hidden.Contains(p.Activity.Id);
        if (ImGui.SmallButton(isHidden ? T("Вернуть в список", "Restore activity") : T("Скрыть эту активность", "Hide this activity"))) plugin.Hide(p.Activity.Id, !isHidden);
    }

    private void DrawQuestAction(NextStep step, bool started)
    {
        ImGui.TextWrapped(step.Reason == "offer" && started
            ? T("Цепочка начата. Возьми следующее задание у NPC.", "Chain started. Accept the next quest from the NPC.")
            : Reason(step));
        if (step.QuestId != 0)
        {
            ImGui.TextColored(Muted, T("Следующее задание", "Next quest"));
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
                var location = locate(step.QuestId);
                if (location != null)
                {
                    ImGui.TextColored(Muted, T("Где взять задание", "Quest giver"));
                    ImGui.TextWrapped(location.Npc);
                    ImGui.TextWrapped($"{location.Zone} · X: {location.X:F1}, Y: {location.Y:F1}");
                    if (ImGui.Button(T("Показать на карте", "Show on map"), new Vector2(-1, 30 * S))) location.Open();
                }
                else ImGui.TextWrapped(T("Точка выдачи пока не определена.", "Starting location is not yet available."));
            }
        }
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
