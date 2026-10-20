using System.Diagnostics;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace BagSense;

public sealed partial class BagWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly bool staticPreview;
    private Text L => plugin.Locale;
    private string T(string ru, string en) => L.T(ru, en);
    private static readonly Vector4 Teal = BagTheme.Accent;
    private static readonly Vector4 Gold = new(.96f, .75f, .41f, 1);
    private static readonly Vector4 Muted = BagTheme.Muted;
    private uint selected;
    private string search = "";
    private int filter;
    private int container;
    private bool reviewMode;
    private bool settings;
    private string note = "";
    private int keepQuantity;
    private int planQuantity = 1;
    private PlanAction action;
    private string goalName = "";
    private string goalQuery = "";
    private string previousQuery = "";
    private string addGoalId = "";
    private List<ItemFacts> goalResults = new();
    private string selectedGoalId = "";
    private int goalQuantity = 1;
    private int requestedTab = -1;

    private float S => ImGui.GetFontSize() / 17f;
    public BagWindow(Plugin plugin, bool staticPreview = false) : base("BagSense###BagSenseMain")
    {
        this.plugin = plugin;
        this.staticPreview = staticPreview;
        Size = new(1240, 860);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(860, 570), MaximumSize = new(2100, 1600) };
    }

    public void ResetSelection()
    {
        selected = 0; note = ""; keepQuantity = 0; selectedGoalId = "";
        reviewMode = false; goalResults.Clear(); previousQuery = "";
    }
    public void Select(uint id)
    {
        requestedTab = 0;
        selected = id;
        filter = 2;
        container = 0;
        search = "";
        LoadSelection();
    }
    private void LoadSelection()
    {
        var data = plugin.Current;
        var snapshot = plugin.Snapshot;
        if (data == null || snapshot == null) return;
        var decision = data.Decision(selected);
        note = decision.Note;
        keepQuantity = decision.KeepQuantity;
        var advice = CleanupPolicy.Evaluate(snapshot, data, selected, DateTimeOffset.UtcNow);
        exchangeKey = "";
        destinationKey = "";
        routeKey = "";
        action = advice.DefaultAction;
        planQuantity = action == PlanAction.Use ? 1 : Math.Max(1, advice.Available);
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, BagTheme.Background);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1f);
    }
    public override void PostDraw() { ImGui.PopStyleVar(); ImGui.PopStyleColor(); }
    public override void Draw()
    {
        ImGui.SetWindowFontScale(plugin.Settings.Scale);
        BagTheme.Push(S);
        try { DrawContent(); }
        finally { BagTheme.Pop(); }
    }

    private void DrawContent()
    {
        ImGui.TextColored(Teal, "BagSense");
        ImGui.SameLine();
        ImGui.TextDisabled(T("Предметы, их назначение и твой маршрут", "Your items, their purpose and your next stop"));
        ImGui.SameLine();
        if (ImGui.SmallButton("RU###RU")) { plugin.Settings.Russian = true; plugin.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("EN###EN")) { plugin.Settings.Russian = false; plugin.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Настройки", "Settings") + "###Settings")) settings = !settings;
        if (settings) DrawSettings();
        if (plugin.Demo)
        {
            ImGui.TextColored(Gold, T("ДЕМОНСТРАЦИЯ — учебные предметы и отдельные временные цели", "DEMO — sample items and separate temporary goals"));
            if (ImGui.SmallButton(T("К моему персонажу", "Use my character") + "###Live")) plugin.SetDemo(false);
            ImGui.SameLine();
            if (ImGui.SmallButton(T("Сбросить пример", "Reset sample") + "###ResetDemo")) plugin.ResetDemo();
        }
        var snapshot = plugin.Snapshot;
        var data = plugin.Current;
        if (snapshot == null || data == null)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(T(plugin.ReadError ? "Инвентарь сейчас недоступен. Дождись загрузки персонажа и нажми обновить."
                : plugin.CatalogError ? "Не удалось прочитать каталог игры. Обновить — повторная попытка."
                : "Войди в игру и дождись загрузки инвентаря. Каталог предметов подготавливается автоматически.",
                plugin.ReadError ? "Inventory is unavailable. Wait for the character to load, then refresh."
                : plugin.CatalogError ? "Could not read the game catalog. Refresh to retry."
                : "Log in and wait for your inventory to load. The item catalog is prepared automatically."));
            if (ImGui.Button(T("Обновить", "Refresh") + "###Retry")) plugin.Refresh();
            ImGui.SameLine();
            if (ImGui.Button(T("Посмотреть пример", "Explore sample") + "###Demo")) plugin.SetDemo(true);
            return;
        }
        ImGui.TextColored(Muted, snapshot.CharacterName);
        ImGui.SameLine();
        ImGui.Text(T($"Сумка {snapshot.BagSlots}/{snapshot.BagCapacity}  ·  Armoury {snapshot.ArmourySlots}",
            $"Bags {snapshot.BagSlots}/{snapshot.BagCapacity}  ·  Armoury {snapshot.ArmourySlots}"));
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Обновить", "Refresh") + "###Scan")) plugin.Refresh();
        ImGui.ProgressBar(snapshot.BagCapacity == 0 ? 0 : (float)snapshot.BagSlots / snapshot.BagCapacity,
            new(-1, 3 * S), "");
        if (ImGui.BeginTabBar("Sections"))
        {
            if (ImGui.BeginTabItem(T("Инвентарь", "Inventory") + "###Inventory", requestedTab == 0 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawInventory(snapshot, data); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T("Мои цели", "My goals") + "###Goals", requestedTab == 1 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawGoals(snapshot, data); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T($"План очистки ({data.Plan.Count})", $"Cleanup plan ({data.Plan.Count})") + "###Plan", requestedTab == 2 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawPlan(snapshot, data); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T("База предметов", "Item database") + "###Database", requestedTab == 3 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawDatabase(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T("Квесты", "Quests") + "###Quests", requestedTab == 4 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawQuests(snapshot); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
            requestedTab = -1;
        }
        ImGui.Separator();
        ImGui.TextDisabled(T($"Обновлено {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}  ·  Действия вручную  ·  Сумки + Armoury",
            $"Updated {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}  ·  Manual actions  ·  Bags + Armoury"));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Ретейнеры, Saddlebag и комод не учитываются. Прогресс и заметки хранятся на этом компьютере.",
            "Retainers, saddlebags and dresser are not scanned. Progress and notes stay on this computer."));
    }

    private void DrawSettings()
    {
        var scale = plugin.Settings.Scale;
        ImGui.SetNextItemWidth(180 * S);
        if (ImGui.SliderFloat(T("Масштаб", "Scale") + "###Scale", ref scale, .85f, 1.5f, "%.2f"))
        { plugin.Settings.Scale = scale; plugin.Save(); }
        ImGui.TextDisabled(T("Категории в списке «Разобрать»:", "Categories shown in Review:"));
        bool gear = plugin.Settings.ShowEquipment, mats = plugin.Settings.ShowMaterials, unlocks = plugin.Settings.ShowCollectibles;
        bool changed = ImGui.Checkbox(T("Экипировка", "Equipment") + "###Gear", ref gear);
        ImGui.SameLine(); changed |= ImGui.Checkbox(T("Материалы и прочее", "Materials and other") + "###Mats", ref mats);
        ImGui.SameLine(); changed |= ImGui.Checkbox(T("Коллекции", "Collectibles") + "###Collectibles", ref unlocks);
        if (changed)
        {
            plugin.Settings.ShowEquipment = gear; plugin.Settings.ShowMaterials = mats; plugin.Settings.ShowCollectibles = unlocks; plugin.Save();
        }
        ImGui.TextWrapped(T("Цели, резервы и план сохраняются локально для каждого персонажа. Учебные данные не сохраняются.",
            "Goals, reservations and plans are saved locally per character. Sample data is not saved."));
        if (ImGui.SmallButton(T("Демонстрация", "Demo") + "###SettingsDemo")) plugin.SetDemo(true);
        ImGui.Separator();
    }

    private void DrawInventory(InventorySnapshot snapshot, CharacterData data)
    {
        ImGui.SetNextItemWidth(150 * S);
        var filters = new[] { T("Разобрать", "Review"), T("Мои запасы", "My stock"), T("Все предметы", "All items") };
        ImGui.Combo("###Filter", ref filter, filters, filters.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(155 * S);
        var containers = new[] { T("Все контейнеры", "All containers"), T("Сумка", "Bags"), "Armoury", T("Надето", "Equipped") };
        ImGui.Combo("###Container", ref container, containers, containers.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180 * S);
        InputHint("###Search", T("Название / ID", "Name / ID"), ref search, 128);
        ImGui.SameLine();
        if (ImGui.Button(T(reviewMode ? "Весь список" : "Разобрать сумку", reviewMode ? "Full list" : "Review my bags") + "###Review"))
        { reviewMode = !reviewMode; filter = 0; selected = 0; }
        var advice = snapshot.Stacks.Select(s => s.ItemId).Distinct()
            .Select(id => CleanupPolicy.Evaluate(snapshot, data, id, DateTimeOffset.UtcNow))
            .Where(a => Match(a, snapshot, data)).OrderBy(a => a.Kind == AdviceKind.Learn ? 0 : a.Kind == AdviceKind.Vendor ? 1 : 2)
            .ThenBy(a => snapshot.Items.GetValueOrDefault(a.ItemId)?.Name).ToList();
        if (advice.All(a => a.ItemId != selected)) { selected = advice.FirstOrDefault()?.ItemId ?? 0; LoadSelection(); }
        float height = Math.Max(180 * S, ImGui.GetContentRegionAvail().Y - 42 * S);
        if (ImGui.BeginChild("ItemList", new(ImGui.GetContentRegionAvail().X * .34f, height), true))
        {
            ImGui.TextColored(Muted, T($"ПРЕДМЕТЫ  /  {advice.Count}", $"ITEMS  /  {advice.Count}"));
            if (advice.Count == 0) ImGui.TextWrapped(T("Здесь пока пусто. Проверь фильтр или открой все предметы.", "Nothing here. Check the filters or open All items."));
            var visible = reviewMode ? advice.Where(a => a.ItemId == selected) : advice;
            foreach (var entry in visible)
            {
                ImGui.PushID((int)entry.ItemId);
                var item = snapshot.Items.GetValueOrDefault(entry.ItemId);
                var start = ImGui.GetCursorScreenPos();
                if (ImGui.Selectable("###Item", selected == entry.ItemId, ImGuiSelectableFlags.None, new(0, 68 * S)))
                { selected = entry.ItemId; LoadSelection(); }
                var end = ImGui.GetCursorScreenPos();
                ImGui.SetCursorScreenPos(start + new Vector2(6, 8) * S); DrawIcon(item, 32);
                ImGui.SetCursorScreenPos(start + new Vector2(48, 7) * S);
                string title = item?.Name ?? $"#{entry.ItemId}";
                int max = Math.Max(18, (int)((ImGui.GetWindowWidth() / S - 70) / 8));
                if (title.Length > max) title = title[..(max - 1)] + "…";
                ImGui.TextUnformatted(title);
                ImGui.SetCursorScreenPos(start + new Vector2(48, 30) * S);
                ImGui.TextColored(entry.CanPlan ? Teal : Muted, $"×{entry.Total}  ·  " + L.Kind(entry.Kind));
                if (data.Plan.Any(p => p.ItemId == entry.ItemId))
                { ImGui.SetCursorScreenPos(start + new Vector2(48, 49) * S); ImGui.TextColored(Gold, T("В плане", "In your plan")); }
                else if (entry.Reserved > 0)
                { ImGui.SetCursorScreenPos(start + new Vector2(48, 49) * S); ImGui.TextDisabled(T($"Сохранить {entry.Reserved}", $"Keep {entry.Reserved}")); }
                ImGui.SetCursorScreenPos(end);
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("ItemDetail", new(0, height), true) && selected != 0)
        {
            var entry = advice.FirstOrDefault(a => a.ItemId == selected);
            if (entry != null) DrawItem(snapshot, data, entry);
        }
        ImGui.EndChild();
    }

    private bool Match(Advice a, InventorySnapshot snapshot, CharacterData data)
    {
        var item = snapshot.Items.GetValueOrDefault(a.ItemId);
        if (container > 0 && !snapshot.Stacks.Any(s => s.ItemId == a.ItemId && (int)s.Container == container - 1)) return false;
        if (search.Length > 0 && !a.ItemId.ToString().Contains(search) && !(item?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)) return false;
        if (filter == 1) return data.Decisions.ContainsKey(a.ItemId) || a.Reserved > 0 || a.GoalNames.Length > 0 || a.Kind == AdviceKind.Protected;
        if (filter == 2) return true;
        if (a.Kind is AdviceKind.Protected or AdviceKind.Reserved or AdviceKind.Deferred) return false;
        if (reviewMode && data.Plan.Any(p => p.ItemId == a.ItemId)) return false;
        if (item?.Equipment == true) return plugin.Settings.ShowEquipment;
        if (item?.Unlocked != null) return plugin.Settings.ShowCollectibles;
        return plugin.Settings.ShowMaterials;
    }

    private void DrawGoalAssignment(CharacterData data, uint itemId)
    {
        var goals = data.Goals.Where(g => !g.Completed).ToList();
        if (goals.Count == 0)
        { ImGui.TextDisabled(T("Создай цель во вкладке «Мои цели», чтобы привязать предмет.", "Create a goal in My goals to reserve this item.")); return; }
        var chosen = goals.FirstOrDefault(g => g.Id == selectedGoalId) ?? goals[0];
        selectedGoalId = chosen.Id;
        ImGui.SetNextItemWidth(180 * S);
        if (ImGui.BeginCombo("###AssignGoal", chosen.Name))
        {
            foreach (var goal in goals)
                if (ImGui.Selectable(goal.Name + "###" + goal.Id, goal.Id == selectedGoalId)) selectedGoalId = goal.Id;
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(100 * S);
        Number("###GoalQuantity", ref goalQuantity);
        goalQuantity = Math.Clamp(goalQuantity, 1, 999999);
        if (ImGui.SmallButton(T("Сохранить количество в цели", "Set goal quantity") + "###Assign"))
        {
            data.Goals.First(g => g.Id == selectedGoalId).Items[itemId] = goalQuantity;
            plugin.Changed();
        }
    }

    private void DrawPlanControls(InventorySnapshot snapshot, CharacterData data, Advice advice)
    {
        // Re-evaluate after edits earlier in this frame so a newly added reserve applies immediately.
        advice = CleanupPolicy.Evaluate(snapshot, data, advice.ItemId, DateTimeOffset.UtcNow);
        var item = snapshot.Items.GetValueOrDefault(advice.ItemId);
        if (!advice.CanPlan || item == null)
        { ImGui.TextWrapped(T("План очистки недоступен для защищённого или отложенного предмета.", "Protected or deferred items cannot be added to cleanup.")); return; }
        var actions = Enum.GetValues<PlanAction>().Where(a => CleanupPolicy.Supports(item, a)).ToArray();
        if (!actions.Contains(action)) action = advice.DefaultAction;
        ImGui.SetNextItemWidth(200 * S);
        if (ImGui.BeginCombo("###Action", L.Action(action)))
        {
            foreach (var available in actions)
                if (ImGui.Selectable(L.Action(available), action == available)) { action = available; destinationKey = ""; exchangeKey = ""; routeKey = ""; }
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(100 * S);
        Number(T("шт.", "units") + "###PlanQuantity", ref planQuantity);
        planQuantity = Math.Clamp(planQuantity, 1, Math.Max(1, advice.Available));
        if (action == PlanAction.Use) planQuantity = 1;
        var projected = CleanupPolicy.Project(snapshot, data,
            new() { ItemId = advice.ItemId, Action = action, Quantity = planQuantity, BaselineQuantity = advice.Total }, DateTimeOffset.UtcNow);
        if (ImGui.GetContentRegionAvail().X > 640 * S) ImGui.SameLine();
        if (ImGui.Button(T("Добавить в план", "Add to my plan") + "###AddPlan"))
        {
            CleanupPolicy.AddToPlan(snapshot, data, advice, action, planQuantity);
            var step = data.Plan.FirstOrDefault(p => p.ItemId == advice.ItemId);
            if (step != null)
            {
                step.DestinationKey = action is PlanAction.Vendor or PlanAction.ExchangeReview ? destinationKey : "";
                step.ExchangeKey = action == PlanAction.ExchangeReview ? exchangeKey : "";
                step.RouteKey = action == PlanAction.ExchangeReview ? routeKey : "";
            }
            plugin.Changed();
        }
        if (action is not (PlanAction.ExchangeReview or PlanAction.ManualReview))
            ImGui.TextDisabled(T($"Можно освободить: {projected.BagSlots} сл. сумки, {projected.ArmourySlots} сл. Armoury.",
                $"Potential space: {projected.BagSlots} bag slots, {projected.ArmourySlots} Armoury slots."));
        if (action == PlanAction.Desynthesize) ImGui.TextWrapped(T("Проверь доступ к разбору. Полученные материалы могут занять новые слоты.",
            "Check desynthesis access. Resulting materials may occupy new slots."));
        if (data.Plan.Any(p => p.ItemId == advice.ItemId))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(T("Убрать из плана", "Remove from plan") + "###Unplan"))
            { data.Plan.RemoveAll(p => p.ItemId == advice.ItemId); plugin.Changed(); }
        }
    }

    private void DrawGoals(InventorySnapshot snapshot, CharacterData data)
    {
        ImGui.SetNextItemWidth(300 * S);
        InputHint("###NewGoal", T("Название новой цели", "New goal name"), ref goalName, 160);
        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(goalName));
        if (ImGui.Button(T("Создать цель", "Create goal") + "###CreateGoal"))
        { data.Goals.Add(new() { Name = goalName.Trim() }); goalName = ""; plugin.Changed(); }
        ImGui.EndDisabled();
        ImGui.TextWrapped(T("Активные цели суммируют нужное количество. Завершённая цель освобождает резерв, сохраняя заметки.",
            "Active goals add their required quantities together. Completing a goal releases its reserve and keeps your notes."));
        if (ImGui.BeginChild("GoalList", new(0, Math.Max(180 * S, ImGui.GetContentRegionAvail().Y - 65 * S)), false))
        {
            if (data.Goals.Count == 0) ImGui.TextWrapped(T("Например: «Прокачать кулинара», «Моя первая реликта», «Подарок другу».",
                "For example: Level Culinarian, My first relic, A gift for a friend."));
            foreach (var goal in data.Goals.ToArray()) DrawGoal(snapshot, data, goal);
        }
        ImGui.EndChild();
    }

    private void DrawGoal(InventorySnapshot snapshot, CharacterData data, Goal goal)
    {
        ImGui.PushID(goal.Id);
        bool completed = goal.Completed;
        if (ImGui.Checkbox(T("Завершена", "Completed") + "###Completed", ref completed)) { goal.Completed = completed; plugin.Changed(); }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300 * S);
        var name = goal.Name;
        if (Input("###GoalName", ref name, 160)) { goal.Name = name; plugin.Changed(); }
        var goalNote = goal.Note;
        ImGui.SetNextItemWidth(-1);
        if (InputHint("###GoalNote", T("Заметка к цели", "Goal note"), ref goalNote, 1024)) { goal.Note = goalNote; plugin.Changed(); }
        foreach (var pair in goal.Items.ToArray())
        {
            ImGui.PushID((int)pair.Key);
            var item = snapshot.Items.GetValueOrDefault(pair.Key) ?? plugin.Catalog?.Find(pair.Key);
            ImGui.TextWrapped(item?.Name ?? $"#{pair.Key}");
            int required = pair.Value;
            ImGui.SetNextItemWidth(120 * S);
            if (Number(T("Нужно", "Needed") + "###Required", ref required))
            { goal.Items[pair.Key] = Math.Clamp(required, 1, 999999); plugin.Changed(); }
            ImGui.SameLine();
            int allocated = CleanupPolicy.Allocated(snapshot, data, goal, pair.Key);
            ImGui.Text(T($"Выделено {allocated}/{goal.Items[pair.Key]} · Всего в наличии {snapshot.Quantity(pair.Key)}",
                $"Allocated {allocated}/{goal.Items[pair.Key]} · Total held {snapshot.Quantity(pair.Key)}"));
            ImGui.SameLine();
            if (ImGui.SmallButton(T("Убрать", "Remove") + "###RemoveGoalItem")) { goal.Items.Remove(pair.Key); plugin.Changed(); }
            ImGui.PopID();
        }
        ImGui.BeginDisabled(goal.Completed);
        if (ImGui.SmallButton(T("Добавить предмет по названию / ID", "Add item by name / ID") + "###AddGoalItem"))
        { addGoalId = goal.Id; goalQuery = ""; previousQuery = ""; goalResults.Clear(); ImGui.OpenPopup("AddGoalItem"); }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Удалить цель…", "Delete goal…") + "###DeleteGoal")) ImGui.OpenPopup("DeleteGoal");
        if (ImGui.BeginPopup("DeleteGoal"))
        {
            ImGui.TextWrapped(T("Удалить цель и её резервы? Заметки предметов останутся.", "Delete this goal and its reservations? Item notes will remain."));
            if (ImGui.Button(T("Удалить", "Delete") + "###ConfirmDelete"))
            { data.Goals.Remove(goal); plugin.Changed(); ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
        // The popup belongs to this goal's ID scope.
        DrawAddItemPopup(data);
        ImGui.TextDisabled(T("Распределение между целями — сверху вниз; одно количество не засчитывается дважды.",
            "Stock is allocated to goals from top to bottom; units are not counted twice."));
        ImGui.Separator();
        ImGui.PopID();
    }

    private void DrawAddItemPopup(CharacterData data)
    {
        if (!ImGui.BeginPopup("AddGoalItem")) return;
        ImGui.SetNextItemWidth(360 * S);
        InputHint("###CatalogQuery", T("Английское название или ID (от 2 знаков)", "English name or ID (at least 2 characters)"), ref goalQuery, 128);
        if (goalQuery != previousQuery)
        {
            previousQuery = goalQuery;
            goalResults = plugin.Demo ? plugin.Snapshot!.Items.Values.Where(i => i.Name.Contains(goalQuery, StringComparison.OrdinalIgnoreCase)).ToList()
                : plugin.Catalog?.Search(goalQuery).ToList() ?? new();
        }
        ImGui.SetNextItemWidth(120 * S);
        Number(T("Количество", "Quantity") + "###AddQuantity", ref goalQuantity);
        goalQuantity = Math.Clamp(goalQuantity, 1, 999999);
        if (ImGui.BeginChild("SearchResults", new(490 * S, 260 * S), true))
        {
            foreach (var item in goalResults)
                if (ImGui.Selectable(item.Name + "###" + item.Id))
                {
                    var goal = data.Goals.FirstOrDefault(g => g.Id == addGoalId);
                    if (goal != null && !goal.Completed) { goal.Items[item.Id] = goalQuantity; plugin.Changed(); }
                    ImGui.CloseCurrentPopup();
                }
            if (goalQuery.Length >= 2 && goalResults.Count == 0) ImGui.TextWrapped(T("Совпадений нет.", "No matching items."));
        }
        ImGui.EndChild();
        ImGui.EndPopup();
    }

    private void DrawPlan(InventorySnapshot snapshot, CharacterData data)
    {
        var now = DateTimeOffset.UtcNow;
        var projection = data.Plan.Select(e => CleanupPolicy.Project(snapshot, data, e, now)).ToArray();
        ImGui.TextColored(Teal, T($"Потенциально освободится: {projection.Sum(p => p.BagSlots)} сл. сумки · {projection.Sum(p => p.ArmourySlots)} сл. Armoury",
            $"Potential space: {projection.Sum(p => p.BagSlots)} bag slots · {projection.Sum(p => p.ArmourySlots)} Armoury slots"));
        ImGui.TextWrapped(T("Это число освобождаемых исходных слотов, не чистый прирост места после разбора или обмена. Выполняй действия вручную, начиная с маленьких стопок.",
            "This counts source slots released, not net space after desynthesis or exchanges. Perform actions manually, starting with small stacks."));
        if (ImGui.Button(T("Скопировать план", "Copy plan") + "###CopyPlan")) ImGui.SetClipboardText(ExportPlan(snapshot, data));
        if (ImGui.BeginChild("PlanSteps", new(0, Math.Max(180 * S, ImGui.GetContentRegionAvail().Y - 65 * S)), false))
        {
            if (data.Plan.Count == 0) ImGui.TextWrapped(T("Добавь предметы из вкладки «Инвентарь». Здесь появятся шаги, сгруппированные по действию.",
                "Add items from Inventory. Your steps will appear here, grouped by action."));
            foreach (var group in data.Plan.ToArray().GroupBy(p =>
                {
                    var fact = snapshot.Items.GetValueOrDefault(p.ItemId) ?? plugin.Catalog?.Find(p.ItemId);
                    var location = fact == null ? null : DestinationPolicy.ForPlan(fact, p, snapshot.TerritoryId);
                    return location == null ? L.Action(p.Action) : location.Npc + " · " + location.Zone;
                }).OrderBy(g => g.Key))
            {
                ImGui.TextColored(Teal, group.Key);
                foreach (var entry in group) DrawPlanEntry(snapshot, data, entry);
            }
        }
        ImGui.EndChild();
    }

    private void DrawPlanEntry(InventorySnapshot snapshot, CharacterData data, PlanEntry entry)
    {
        ImGui.PushID((int)entry.ItemId);
        var item = snapshot.Items.GetValueOrDefault(entry.ItemId) ?? plugin.Catalog?.Find(entry.ItemId);
        var projection = CleanupPolicy.Project(snapshot, data, entry, DateTimeOffset.UtcNow);
        ImGui.TextWrapped($"{item?.Name ?? $"#{entry.ItemId}"} ×{entry.Quantity}");
        ImGui.TextDisabled(L.Action(entry.Action));
        if (item != null) DrawPlanDestination(snapshot, item, entry);
        if (projection.Stale) ImGui.TextColored(Gold, T("Количество уменьшилось — проверь шаг заново.", "Quantity decreased — review this step again."));
        if (projection.Blocked) ImGui.TextWrapped(T("Шаг заблокирован: резерв, защита, изучение или доступность предмета изменились.",
            "Step blocked: reservations, protection, unlock state or item availability changed."));
        else ImGui.TextWrapped(T($"Свободно для шага: {projection.Quantity} шт. · Исходные слоты: сумка {projection.BagSlots}, Armoury {projection.ArmourySlots}",
            $"Available items for this step: {projection.Quantity} · Source slots: bags {projection.BagSlots}, Armoury {projection.ArmourySlots}"));
        if (projection.Quantity < entry.Quantity && !projection.Blocked) ImGui.TextColored(Gold, T("Объём шага ограничен текущим свободным запасом.", "Step quantity is limited by the current surplus."));
        if (!projection.Blocked && (projection.Stale || projection.Quantity < entry.Quantity))
        {
            if (ImGui.SmallButton(T("Проверил — обновить шаг", "Reviewed — update step") + "###Rebase"))
            { entry.Quantity = projection.Quantity; entry.BaselineQuantity = snapshot.Quantity(entry.ItemId); entry.ObservedDecrease = false; plugin.Changed(); }
            ImGui.SameLine();
        }
        if (ImGui.SmallButton(T("Готово / убрать шаг", "Done / remove step") + "###RemoveStep"))
        { data.Plan.Remove(entry); plugin.Changed(); }
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Карточка предмета", "Item details") + "###EditItem")) Select(entry.ItemId);
        ImGui.Separator();
        ImGui.PopID();
    }

    private string ExportPlan(InventorySnapshot snapshot, CharacterData data)
    {
        var text = new StringBuilder("BagSense — " + snapshot.CharacterName + Environment.NewLine);
        foreach (var group in data.Plan.GroupBy(e => e.Action))
        {
            text.AppendLine(L.Action(group.Key));
            foreach (var entry in group)
            {
                var projection = CleanupPolicy.Project(snapshot, data, entry, DateTimeOffset.UtcNow);
                string name = snapshot.Items.GetValueOrDefault(entry.ItemId)?.Name ?? plugin.Catalog?.Find(entry.ItemId)?.Name ?? $"#{entry.ItemId}";
                text.AppendLine($"- {name} ×{projection.Quantity}" + (projection.Blocked ? T(" [заблокирован]", " [blocked]") : projection.Stale ? T(" [проверить заново]", " [review again]") : ""));
                var item = snapshot.Items.GetValueOrDefault(entry.ItemId) ?? plugin.Catalog?.Find(entry.ItemId);
                if (item != null)
                {
                    var location = DestinationPolicy.ForPlan(item, entry, snapshot.TerritoryId);
                    if (location != null) text.AppendLine($"  {location.Npc} · {location.Zone} (X: {location.X:0.0}, Y: {location.Y:0.0})");
                    var exchange = item.ExchangeUses.FirstOrDefault(e => e.Key == entry.ExchangeKey);
                    if (exchange != null) text.AppendLine("  " + exchange.Reward + " ← " + string.Join(" + ", exchange.FullCosts.Select(c => $"{c.Count} × {c.Name}")));
                }
            }
        }
        return text.ToString();
    }

    private string ContainerName(ContainerKind kind) => kind switch
    { ContainerKind.Bag => T("Сумка", "Bags"), ContainerKind.Armoury => "Armoury", _ => T("Надето", "Equipped") };
    // Dalamud's native InputText extension needs its hosted text-input environment.
    // Standalone rendering uses inert fields; game input always uses the real widgets.
    private bool Input(string label, ref string value, int maximum) => InputHint(label, "", ref value, maximum);
    private bool InputHint(string label, string hint, ref string value, int maximum)
    {
        if (!staticPreview) return ImGui.InputTextWithHint(label, hint, ref value, maximum);
        ImGui.BeginDisabled();
        ImGui.Button((value.Length == 0 ? hint : value) + label, new(ImGui.CalcItemWidth(), 0));
        ImGui.EndDisabled();
        return false;
    }
    private bool Number(string label, ref int value)
    {
        if (!staticPreview) return ImGui.InputInt(label, ref value);
        ImGui.BeginDisabled(); ImGui.Button(value + "###Preview" + label, new(ImGui.CalcItemWidth(), 0)); ImGui.EndDisabled();
        var visible = label.Split("###")[0];
        if (visible.Length > 0) { ImGui.SameLine(); ImGui.TextUnformatted(visible); }
        return false;
    }
    private void DrawIcon(ItemFacts? item, float size)
    {
        if (item == null || !DrawGameImage(item.Icon, new(size * S, size * S))) ImGui.Dummy(new(size * S, size * S));
    }
    public void Dispose() { }
}
