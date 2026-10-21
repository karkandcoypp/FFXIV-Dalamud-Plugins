using Dalamud.Bindings.ImGui;

namespace BagSense;

public sealed partial class BagWindow
{
    private string databaseQuery = "";
    private int databasePage;
    private uint databaseItem;

    private void DrawDatabase()
    {
        var catalog = plugin.Catalog;
        if (catalog == null) { ImGui.TextWrapped(T("База загружается…", "Loading database…")); return; }
        var database = catalog.Database;
        ImGui.TextColored(Teal, T("Полный каталог предметов", "Complete item catalog"));
        ImGui.TextWrapped(database == null
            ? T("Данные установленной игры. Онлайн-каталог ещё не загружен.", "Installed game data. Online catalog has not loaded yet.")
            : T($"{database.Items.Length:N0} предметов · {database.Quests?.Length ?? 0:N0} квестов · {database.Needs.Count(n => n.Count.HasValue):N0} известных количеств",
                $"{database.Items.Length:N0} items · {database.Quests?.Length ?? 0:N0} quests · {database.Needs.Count(n => n.Count.HasValue):N0} known quantities"));
        if (database != null) ImGui.TextDisabled(T($"Обновлено {database.GeneratedAt.ToLocalTime():dd.MM.yyyy HH:mm} · Источник: ffxiv-datamining / GitHub",
            $"Updated {database.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm} · Source: ffxiv-datamining / GitHub"));
        if (plugin.DatabaseOffline) ImGui.TextWrapped(T("Сеть недоступна. Используется сохранённая база или данные игры.", "Offline. Using the saved catalog or installed game data."));
        ImGui.BeginDisabled(plugin.DatabaseBusy || staticPreview);
        if (ImGui.Button(T(plugin.DatabaseBusy ? "Обновляется…" : "Проверить обновления базы", plugin.DatabaseBusy ? "Updating…" : "Check database updates") + "###DatabaseRefresh")) plugin.RefreshDatabase();
        ImGui.EndDisabled();
        if (database?.Sources is { } sources) ImGui.TextWrapped(T($"Дополнительно: {sources.Length:N0} связей получения из Teamcraft. Полнота описаний различается; отсутствие связи не разрешает удаление.",
            $"Also: {sources.Length:N0} Teamcraft acquisition links. Coverage varies; missing links do not authorize disposal."));
        ImGui.TextDisabled(T("Автопроверка: при загрузке и раз в сутки. Данные персонажа не отправляются.", "Auto-check: on load and daily. Character data is not uploaded."));
        ImGui.SetNextItemWidth(340 * S);
        if (InputHint("###DatabaseSearch", T("Название предмета / ID", "Item name / ID"), ref databaseQuery, 128)) databasePage = 0;
        var rows = catalog.Browse(databaseQuery.Trim(), databasePage * 50, 51);
        ImGui.BeginDisabled(databasePage == 0);
        if (ImGui.SmallButton(T("Назад", "Previous") + "###DbBack")) databasePage--;
        ImGui.EndDisabled(); ImGui.SameLine(); ImGui.Text($"{databasePage + 1}"); ImGui.SameLine();
        ImGui.BeginDisabled(rows.Length <= 50);
        if (ImGui.SmallButton(T("Далее", "Next") + "###DbNext")) databasePage++;
        ImGui.EndDisabled();
        float width = ImGui.GetContentRegionAvail().X;
        ImGui.BeginChild("DbItems", new(width * .38f, -34 * S), true);
        foreach (var row in rows.Take(50))
            if (ImGui.Selectable(row.Name + "###Db" + row.Id, row.Id == databaseItem)) databaseItem = row.Id;
        if (rows.Length == 0) ImGui.TextWrapped(T("Ничего не найдено.", "No matches."));
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("DbDetail", new(0, -34 * S), true);
        var item = catalog.Find(databaseItem);
        if (item != null)
        {
            ImGui.TextColored(Teal, item.Name);
            ImGui.TextDisabled($"ID {item.Id} · {item.Category}");
            ImGui.TextWrapped(L.Purpose(item));
            DrawIdentity(item, plugin.Catalog?.Bundles(item.Id).Length > 0);
            if (plugin.Snapshot is { } snapshot)
            {
                if (item.Reward is { Kind: not "Item" }) DrawRewardCard(item, 1, snapshot);
                ImGui.TextWrapped(T($"В сумках: {snapshot.Quantity(item.Id)}", $"In bags: {snapshot.Quantity(item.Id)}"));
                if (snapshot.Quantity(item.Id) > 0 && ImGui.Button(T("Открыть в инвентаре", "Open in inventory") + "###DbInventory")) Select(item.Id);
                DrawBundles(item, snapshot);
                DrawResearch(item, snapshot);
                DrawAcquisition(item, snapshot);
                DrawDeliveries(item, snapshot);
                if (item.QuestUses.Any(q => !q.Reward)) DrawQuestUses(item, snapshot, false);
                else ImGui.TextWrapped(T("Квестовые требования не найдены. Это не доказательство, что предмет не нужен.",
                    "No quest requirements found. This does not prove the item is unneeded."));
            }
        }
        else ImGui.TextWrapped(T("Выбери предмет слева.", "Choose an item on the left."));
        ImGui.EndChild();
    }
}
