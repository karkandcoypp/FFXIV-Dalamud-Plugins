using Dalamud.Bindings.ImGui;
namespace CraftProfile;
internal sealed class ProfileWindow(Plugin plugin)
{
    public bool IsOpen;
    private string T(string ru, string en) => plugin.Settings.Russian ? ru : en;
    public void Draw()
    {
        if (!IsOpen) return;
        ImGui.SetNextWindowSize(new(960, 680), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("CraftProfile###CraftProfileMain", ref IsOpen)) { ImGui.End(); return; }
        try
        {
            bool russian = plugin.Settings.Russian;
            if (ImGui.Checkbox("Русский", ref russian)) { plugin.Settings.Russian = russian; plugin.SaveSettings(); }
            ImGui.SameLine();
            bool auto = plugin.Settings.AutoExport;
            if (ImGui.Checkbox(T("Автоэкспорт", "Auto export"), ref auto)) { plugin.Settings.AutoExport = auto; plugin.SaveSettings(); }
            ImGui.SameLine();
            if (ImGui.Button(T("Обновить", "Refresh"))) plugin.Refresh();
            if (ImGui.Button(T("Скопировать путь", "Copy path"))) ImGui.SetClipboardText(plugin.LatestFile);
            ImGui.TextWrapped(plugin.LatestFile);
            ImGui.TextWrapped(T("Все данные остаются на компьютере. Сохранённый комплект не подтверждает текущие характеристики. Для замера один раз надень нужную профессию.",
                "All data stays local. Saved gearsets do not verify current stats. Equip each profession once for an observed measurement."));
            if (plugin.Error != null) ImGui.TextWrapped(T("Ошибка: ", "Error: ") + plugin.Error);
            if (plugin.Profile is not { } profile) { ImGui.TextWrapped(T("Ожидание персонажа и загруженного инвентаря.", "Waiting for character and loaded inventory.")); return; }
            ImGui.Separator();
            ImGui.TextUnformatted($"{profile.CharacterName} | {profile.GeneratedAt.ToLocalTime():g}");
            ImGui.TextUnformatted(T("Рецепты: ", "Recipes: ") + plugin.RecipeCount + (plugin.CatalogComplete ? T(" — готовы", " — complete") : T(" — загрузка...", " — loading...")));
            if (ImGui.BeginTable("jobs", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new(0, 280)))
            {
                foreach (string header in new[] { T("Профессия", "Job"), T("Уровень", "Level"), T("Специалист", "Specialist"), T("Замер", "Observed"), "Craft / Control / CP", "Gather / Perception / GP" }) ImGui.TableSetupColumn(header);
                ImGui.TableHeadersRow();
                foreach (var job in profile.Professions)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(job.Abbreviation);
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(job.Level.ToString());
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(job.SpecialistAssigned is true ? T("Да", "Yes") : job.SpecialistAssigned is false ? T("Нет", "No") : "?");
                    ImGui.TableNextColumn();
                    if (!profile.Observations.TryGetValue(job.JobId, out var observed)) { ImGui.TextUnformatted(T("Нет", "Missing")); continue; }
                    ImGui.TextUnformatted(observed.CapturedAt.ToLocalTime().ToString("g"));
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{observed.Stats.Craftsmanship} / {observed.Stats.Control} / {observed.Stats.Cp}");
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{observed.Stats.Gathering} / {observed.Stats.Perception} / {observed.Stats.Gp}");
                }
                ImGui.EndTable();
            }
            if (ImGui.CollapsingHeader(T("Комплекты и материя", "Gearsets and materia")))
                foreach (var set in profile.SavedGearsets)
                    if (ImGui.TreeNode($"{set.Number}: {set.Name} | ilvl {set.ItemLevel}###set{set.Number}"))
                    {
                        foreach (var item in set.Items)
                        {
                            ImGui.TextUnformatted($"[{item.Slot}] {item.Name} {(item.Hq ? "HQ" : "NQ")}");
                            foreach (var materia in item.Materia) ImGui.BulletText($"{materia.Name ?? $"type {materia.TypeId}"} +{materia.Value} (slot {materia.Slot + 1})");
                        }
                        ImGui.TreePop();
                    }
            if (ImGui.CollapsingHeader(T("Книги", "Books")))
                foreach (var book in profile.MasterBooks) ImGui.TextUnformatted($"{(book.Unlocked is true ? "+" : book.Unlocked is false ? "-" : "?")} {book.Name}");
            if (ImGui.CollapsingHeader(T("Ограничения и полнота", "Limitations and coverage")))
            {
                ImGui.TextWrapped(T("Ретейнеры и их продажи неизвестны. Ротация, вероятность HQ и стоимость улучшений ещё не проверены. Характеристики включают активные эффекты; снимок без бафов сохраняется отдельно только при отсутствии статусов.",
                    "Retainers and their sales are unknown. Rotation, HQ probability and upgrade costs are unverified. Observed stats include active effects; a baseline is saved separately only with no statuses."));
                foreach (var container in profile.InventoryCoverage) ImGui.TextUnformatted($"{container.Container}: {(container.Loaded ? container.Slots.ToString() : T("неизвестно", "unknown"))}");
            }
        }
        finally { ImGui.End(); }
    }
}
