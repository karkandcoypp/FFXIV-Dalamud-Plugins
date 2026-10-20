using Dalamud.Bindings.ImGui;

namespace BagSense;

public sealed partial class BagWindow
{
    private string questQuery = "";
    private bool onlyActiveQuests;
    private int questPage;
    private uint selectedQuest;

    private void DrawQuests(InventorySnapshot snapshot)
    {
        var database = plugin.Catalog?.Database;
        var quests = database?.Quests;
        if (quests == null)
        {
            ImGui.TextWrapped(T("Расширенный каталог квестов ещё не загружен. Проверь обновление базы во вкладке «База предметов».",
                "The extended quest catalog has not loaded. Check for updates in Item database."));
            return;
        }
        ImGui.TextColored(Teal, T($"Все доступные квесты: {quests.Length:N0}", $"All available quests: {quests.Length:N0}"));
        ImGui.TextWrapped(T("Задачи, предметы и NPC из данных игры. Неизвестное количество сохраняет весь запас активного квеста.",
            "Objectives, items and NPCs from game data. Unknown quantities protect all stock for an active quest."));
        ImGui.SetNextItemWidth(310 * S);
        if (InputHint("###QuestQuery", T("Название квеста / ID", "Quest name / ID"), ref questQuery, 128)) questPage = 0;
        ImGui.SameLine();
        if (ImGui.Checkbox(T("Только активные", "Active only") + "###ActiveQuests", ref onlyActiveQuests)) questPage = 0;
        bool numeric = uint.TryParse(questQuery, out uint id);
        var matches = quests.Where(q => (!onlyActiveQuests || snapshot.ActiveQuests?.ContainsKey(q.QuestId) == true)
            && (questQuery.Length == 0 || (numeric ? q.QuestId == id : q.Name.Contains(questQuery, StringComparison.OrdinalIgnoreCase)))).ToArray();
        ImGui.BeginDisabled(questPage == 0);
        if (ImGui.SmallButton(T("Назад", "Previous") + "###QuestBack")) questPage--;
        ImGui.EndDisabled(); ImGui.SameLine(); ImGui.Text($"{questPage + 1}"); ImGui.SameLine();
        ImGui.BeginDisabled((questPage + 1) * 40 >= matches.Length);
        if (ImGui.SmallButton(T("Далее", "Next") + "###QuestNext")) questPage++;
        ImGui.EndDisabled();
        float width = ImGui.GetContentRegionAvail().X;
        ImGui.BeginChild("QuestList", new(width * .35f, -34 * S), true);
        foreach (var q in matches.Skip(questPage * 40).Take(40))
            if (ImGui.Selectable(q.Name + "###Quest" + q.QuestId, selectedQuest == q.QuestId)) selectedQuest = q.QuestId;
        if (matches.Length == 0) ImGui.TextWrapped(T("Подходящих квестов нет.", "No matching quests."));
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("QuestDetails", new(0, -34 * S), true);
        var quest = quests.FirstOrDefault(q => q.QuestId == selectedQuest);
        if (quest != null)
        {
            ImGui.TextColored(Teal, quest.Name);
            bool active = snapshot.ActiveQuests?.ContainsKey(quest.QuestId) == true;
            ImGui.TextWrapped(active ? T("Активен · нужные предметы защищены", "Active · required items are protected")
                : T("Сейчас не активен. Для будущего прохождения создай личную цель.", "Not active. Create a personal goal if you are preparing for this quest."));
            var needs = database!.Needs.Where(n => n.QuestId == quest.QuestId).ToArray();
            Heading("Что оставить для квеста", "What to keep for this quest");
            if (needs.Length == 0) ImGui.TextWrapped(T("Явные предметы для сдачи не найдены. Прочитай задачи ниже: квест может использовать особый сценарий.",
                "No explicit hand-in items found. Check the objectives below; this quest may use a special script."));
            foreach (var need in needs)
            {
                ImGui.PushID((int)need.ItemId);
                var item = plugin.Catalog?.Find(need.ItemId);
                bool otherJob = active && item?.QuestUses.FirstOrDefault(q => q.QuestId == quest.QuestId && !q.Reward) is { } use
                    && QuestPolicy.Amount(snapshot, use) == 0;
                if (item != null) { DrawIcon(item, 28); ImGui.SameLine(); }
                ImGui.TextWrapped((item?.Name ?? $"#{need.ItemId}") + " · " + (need.Count.HasValue
                    ? T($"оставить {need.Count}", $"keep {need.Count}") : T("весь запас: количество неизвестно", "all stock: quantity unknown"))
                    + (need.Quality == "hq" ? " HQ" : ""));
                if (need.EventItem) ImGui.TextDisabled(T("Ключевой предмет: хранится отдельно от обычной сумки.", "Key item: stored separately from ordinary bags."));
                if (otherJob) ImGui.TextDisabled(T("Для другой профессии — в принятом варианте не нужен.", "Different job — not required by your accepted variant."));
                foreach (var variant in need.Variants ?? []) ImGui.TextWrapped($"{variant.Job}: {variant.Count}" + (variant.Hq ? " HQ" : ""));
                if (snapshot.Quantity(need.ItemId) > 0 && ImGui.SmallButton(T("Открыть предмет", "Open item") + "###QuestInventory")) Select(need.ItemId);
                ImGui.PopID();
            }
            Heading("Этапы и куда идти", "Objectives and destinations");
            if (quest.Steps.Length == 0) ImGui.TextWrapped(T("Тексты задач отсутствуют в источнике.", "Objective text is unavailable in the source."));
            foreach (var step in quest.Steps)
            {
                ImGui.PushID(step.Index);
                ImGui.Separator();
                ImGui.TextWrapped(T($"Задача {step.Index + 1}: ", $"Objective {step.Index + 1}: ") + step.Text);
                if (active && snapshot.ActiveQuests![quest.QuestId] == 255 && step.CompleteSequence == 255)
                    ImGui.TextColored(Gold, T("Финальная задача текущей фазы", "Final objective of the current phase"));
                foreach (var location in step.Contacts.DistinctBy(l => l.Key)) DrawNpc(location);
                if (step.Contacts.Length == 0) ImGui.TextDisabled(T("Точка NPC не подтверждена для этой задачи.", "NPC location is unconfirmed for this objective."));
                ImGui.PopID();
            }
        }
        else ImGui.TextWrapped(T("Выбери квест слева.", "Choose a quest on the left."));
        ImGui.EndChild();
    }
}
