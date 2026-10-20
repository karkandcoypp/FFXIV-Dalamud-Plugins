using System.Diagnostics;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace BagSense;

public sealed class BagWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly bool staticPreview;
    private Text L => plugin.Locale;
    private string T(string ru, string en) => L.T(ru, en);
    private static readonly Vector4 Teal = new(.40f, .85f, .74f, 1);
    private static readonly Vector4 Gold = new(.96f, .75f, .41f, 1);
    private static readonly Vector4 Muted = new(.65f, .69f, .75f, 1);
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
        Size = new(1120, 760);
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
        action = advice.DefaultAction;
        planQuantity = action == PlanAction.Use ? 1 : Math.Max(1, advice.Available);
    }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(plugin.Settings.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(9, 8) * S);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.075f, .09f, .12f, .96f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.13f, .24f, .26f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.18f, .35f, .36f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.13f, .29f, .28f, 1));
        try { DrawContent(); }
        finally { ImGui.PopStyleColor(4); ImGui.PopStyleVar(3); }
    }

    private void DrawContent()
    {
        ImGui.TextColored(Teal, "B A G S E N S E");
        ImGui.SameLine();
        ImGui.TextDisabled(T("У каждой вещи есть причина", "Give every item a reason"));
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
            new(-1, 6 * S), "");
        if (ImGui.BeginTabBar("Sections"))
        {
            if (ImGui.BeginTabItem(T("Инвентарь", "Inventory") + "###Inventory", requestedTab == 0 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawInventory(snapshot, data); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T("Мои цели", "My goals") + "###Goals", requestedTab == 1 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawGoals(snapshot, data); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T($"План очистки ({data.Plan.Count})", $"Cleanup plan ({data.Plan.Count})") + "###Plan", requestedTab == 2 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { DrawPlan(snapshot, data); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
            requestedTab = -1;
        }
        ImGui.Separator();
        ImGui.TextDisabled(T("Все действия выполняешь ты. Предметы и цены — из установленной игры; интерфейс RU / EN.",
            "You perform all actions. Item names and prices come from your installed game. RU / EN interface."));
        ImGui.TextDisabled(T($"Снимок: {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}  ·  Ретейнеры, Saddlebag и комод не учитываются.",
            $"Snapshot: {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}  ·  Retainers, saddlebags and dresser are not scanned."));
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
        float height = Math.Max(180 * S, ImGui.GetContentRegionAvail().Y - 65 * S);
        if (ImGui.BeginChild("ItemList", new(ImGui.GetContentRegionAvail().X * .43f, height), true))
        {
            ImGui.TextColored(Muted, T($"ПРЕДМЕТЫ  /  {advice.Count}", $"ITEMS  /  {advice.Count}"));
            if (advice.Count == 0) ImGui.TextWrapped(T("Здесь пока пусто. Проверь фильтр или открой все предметы.", "Nothing here. Check the filters or open All items."));
            var visible = reviewMode ? advice.Where(a => a.ItemId == selected) : advice;
            foreach (var entry in visible)
            {
                ImGui.PushID((int)entry.ItemId);
                var item = snapshot.Items.GetValueOrDefault(entry.ItemId);
                DrawIcon(item, 30);
                ImGui.SameLine();
                if (ImGui.Selectable($"{item?.Name ?? $"#{entry.ItemId}"}  ×{entry.Total}###Item", selected == entry.ItemId))
                { selected = entry.ItemId; LoadSelection(); }
                ImGui.TextColored(entry.CanPlan ? Teal : Muted, L.Kind(entry.Kind));
                if (entry.Reserved > 0) ImGui.TextDisabled(T($"Резерв {entry.Reserved} · Остаток {entry.Available}", $"Reserve {entry.Reserved} · Surplus {entry.Available}"));
                if (data.Plan.Any(p => p.ItemId == entry.ItemId)) ImGui.TextColored(Gold, T("В плане", "In your plan"));
                ImGui.Separator();
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

    private void DrawItem(InventorySnapshot snapshot, CharacterData data, Advice advice)
    {
        var item = snapshot.Items.GetValueOrDefault(advice.ItemId);
        DrawIcon(item, 42);
        ImGui.SameLine();
        ImGui.TextWrapped(item?.Name ?? $"#{advice.ItemId}");
        ImGui.TextDisabled($"{item?.Category}  ·  ID {advice.ItemId}");
        ImGui.TextColored(Teal, L.Kind(advice.Kind));
        ImGui.TextWrapped(L.Reason(advice.Reason));
        ImGui.Text(T($"В наличии: {advice.Total}  ·  Резерв: {advice.Reserved}  ·  Остаток: {advice.Available}",
            $"Owned: {advice.Total}  ·  Reserve: {advice.Reserved}  ·  Surplus: {advice.Available}"));
        if (advice.GoalNames.Length > 0) ImGui.TextWrapped(T("Цели: ", "Goals: ") + string.Join(", ", advice.GoalNames));
        if (snapshot.Gearsets.TryGetValue(advice.ItemId, out var sets)) ImGui.TextWrapped(T("Комплекты: ", "Gearsets: ") + string.Join(", ", sets));
        ImGui.TextDisabled(string.Join(" · ", snapshot.Stacks.Where(s => s.ItemId == advice.ItemId).GroupBy(s => s.Container)
            .Select(g => ContainerName(g.Key) + " ×" + g.Sum(s => s.Quantity))));
        if (snapshot.Stacks.Any(s => s.ItemId == advice.ItemId && s.Hq))
            ImGui.TextWrapped(T("Есть HQ. Резерв общий для HQ и NQ; качество нужных экземпляров проверь вручную.", "Includes HQ. Reservations share HQ and NQ; choose the quality to keep manually."));
        ImGui.Separator();
        ImGui.TextColored(Gold, T("ЗАЧЕМ ХРАНЮ", "WHY I KEEP IT"));
        ImGui.SetNextItemWidth(-1);
        if (InputHint("###Note", T("Причина хранения или личная заметка", "A reason to keep it or a personal note"), ref note, 1024))
        { data.Edit(advice.ItemId).Note = note; plugin.Changed(); }
        bool keep = data.Decision(advice.ItemId).AlwaysKeep;
        if (ImGui.Checkbox(T("Всегда сохранять", "Always keep") + "###Keep", ref keep))
        { data.Edit(advice.ItemId).AlwaysKeep = keep; plugin.Changed(); }
        ImGui.SetNextItemWidth(110 * S);
        if (Number(T("Личный запас", "Personal reserve") + "###Reserve", ref keepQuantity))
        { keepQuantity = Math.Clamp(keepQuantity, 0, 999999); data.Edit(advice.ItemId).KeepQuantity = keepQuantity; plugin.Changed(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Резерв = большее из личного запаса и суммы активных целей.", "Reserve = the larger of personal stock and all active goals combined."));
        ImGui.Separator();
        DrawPlanControls(snapshot, data, advice);
        if (ImGui.SmallButton(T("Отложить на день", "Later (one day)") + "###Later"))
        { data.Edit(advice.ItemId).DeferUntil = DateTimeOffset.UtcNow.AddDays(1); plugin.Changed(); selected = 0; }
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Вернуть в разбор", "Review again") + "###Undefer"))
        { data.Edit(advice.ItemId).DeferUntil = null; plugin.Changed(); }
        if (reviewMode)
        {
            if (ImGui.Button(T("Следующий предмет", "Next item") + "###Next"))
            {
                data.Edit(advice.ItemId).DeferUntil = DateTimeOffset.UtcNow.AddHours(1);
                plugin.Changed(); selected = 0;
            }
            ImGui.TextDisabled(T("«Следующий» откладывает решение на час.", "Next defers this decision for one hour."));
        }
        if (ImGui.CollapsingHeader(T("Привязать к цели", "Assign to a goal") + "###GoalAssignment")) DrawGoalAssignment(data, advice.ItemId);
        if (item != null) DrawFacts(item);
    }

    private void DrawFacts(ItemFacts item)
    {
        if (item.BuyOffers.Length > 0 && ImGui.CollapsingHeader(T("Повторное получение в магазине", "Merchant reacquisition") + "###Shops"))
        {
            foreach (var offer in item.BuyOffers)
                ImGui.TextWrapped($"• {offer.Shop} — {offer.Price} gil" + (offer.Conditional ? T(" · есть условия", " · conditional") : ""));
            ImGui.TextWrapped(T("Это записи ассортимента. Доступ к NPC не проверен. Цена относится к обычному качеству; возможны отличия магазина.",
                "These are shop listings. NPC access is not checked. The price is for normal quality; shop differences may apply."));
        }
        if (item.RecipeUses.Length > 0 && ImGui.CollapsingHeader(T("Примеры рецептов", "Example recipes") + "###Recipes"))
            foreach (var recipe in item.RecipeUses) ImGui.TextWrapped("• " + recipe);
        if (item.ExchangeUses.Length > 0 && ImGui.CollapsingHeader(T("Примеры обменов", "Example exchanges") + "###Exchanges"))
        {
            foreach (var exchange in item.ExchangeUses) ImGui.TextWrapped($"• {exchange.Shop}: {exchange.Reward} — {exchange.Cost}");
            ImGui.TextWrapped(T("Список неполный. У обмена могут быть другие затраты, условия и требования качества.",
                "This list is incomplete. Exchanges may have additional costs, conditions and quality requirements."));
        }
        if (item.Description.Length > 0 && ImGui.CollapsingHeader(T("Описание игры", "Game description") + "###Description")) ImGui.TextWrapped(item.Description);
        if (!plugin.Demo && ImGui.SmallButton(T("Открыть Teamcraft", "Open Teamcraft") + "###Teamcraft"))
            Process.Start(new ProcessStartInfo($"https://ffxivteamcraft.com/db/en/item/{item.Id}") { UseShellExecute = true });
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
                if (ImGui.Selectable(L.Action(available), action == available)) action = available;
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(100 * S);
        Number("###PlanQuantity", ref planQuantity);
        planQuantity = Math.Clamp(planQuantity, 1, Math.Max(1, advice.Available));
        if (action == PlanAction.Use) planQuantity = 1;
        var projected = CleanupPolicy.Project(snapshot, data,
            new() { ItemId = advice.ItemId, Action = action, Quantity = planQuantity, BaselineQuantity = advice.Total }, DateTimeOffset.UtcNow);
        ImGui.TextWrapped(T($"Потенциально: {projected.BagSlots} сл. сумки, {projected.ArmourySlots} сл. Armoury.",
            $"Potential: {projected.BagSlots} bag slots, {projected.ArmourySlots} Armoury slots."));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Расчёт для удаления самых маленьких стопок. Обмен и ручная проверка не обещают слоты.",
            "Assumes smallest stacks are removed first. Exchange and review steps promise no slots."));
        if (action == PlanAction.Vendor) ImGui.TextWrapped(T("Цена продажи указана для NQ. Проверь рецепты, HQ и личные планы перед продажей.",
            "Vendor price is for NQ. Review recipes, HQ and your plans before selling."));
        if (action == PlanAction.Desynthesize) ImGui.TextWrapped(T("Данные игры допускают разбор. Профессия и ограничения персонажа не проверены; результат может занять новые слоты.",
            "Game data allows desynthesis. Job access is not checked; the resulting materials may occupy new slots."));
        if (ImGui.Button(T("Добавить / обновить шаг", "Add / update step") + "###AddPlan"))
        { CleanupPolicy.AddToPlan(snapshot, data, advice, action, planQuantity); plugin.Changed(); }
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
            foreach (var group in data.Plan.ToArray().GroupBy(p => p.Action).OrderBy(g => g.Key))
            {
                ImGui.TextColored(Gold, L.Action(group.Key));
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
        if (projection.Stale) ImGui.TextColored(Gold, T("Количество уменьшилось — проверь шаг заново.", "Quantity decreased — review this step again."));
        if (projection.Blocked) ImGui.TextWrapped(T("Шаг заблокирован: резерв, защита, изучение или доступность предмета изменились.",
            "Step blocked: reservations, protection, unlock state or item availability changed."));
        else ImGui.TextWrapped(T($"Сейчас допустимо: {projection.Quantity} · Слоты: сумка {projection.BagSlots}, Armoury {projection.ArmourySlots}",
            $"Currently allowed: {projection.Quantity} · Slots: bags {projection.BagSlots}, Armoury {projection.ArmourySlots}"));
        if (projection.Quantity < entry.Quantity && !projection.Blocked) ImGui.TextColored(Gold, T("Объём шага ограничен текущим свободным запасом.", "Step quantity is limited by the current surplus."));
        if (!projection.Blocked && (projection.Stale || projection.Quantity < entry.Quantity))
        {
            if (ImGui.SmallButton(T("Проверил — обновить шаг", "Reviewed — update step") + "###Rebase"))
            { entry.Quantity = projection.Quantity; entry.BaselineQuantity = snapshot.Quantity(entry.ItemId); entry.ObservedDecrease = false; plugin.Changed(); }
            ImGui.SameLine();
        }
        if (ImGui.SmallButton(T("Готово / убрать шаг", "Done / remove step") + "###RemoveStep"))
        { data.Plan.Remove(entry); plugin.Changed(); }
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
        var texture = item?.Icon > 0 ? Plugin.Textures.GetFromGameIcon(new GameIconLookup(item.Icon)).GetWrapOrDefault() : null;
        if (texture != null) ImGui.Image(texture.Handle, new(size * S, size * S));
        else ImGui.Dummy(new(size * S, size * S));
    }
    public void Dispose() { }
}
