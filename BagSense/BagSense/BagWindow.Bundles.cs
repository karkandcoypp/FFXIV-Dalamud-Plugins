using Dalamud.Bindings.ImGui;
namespace BagSense;

public sealed partial class BagWindow
{
    private string selectedBundle = "";
    private void DrawBundles(ItemFacts item, InventorySnapshot snapshot, uint questOnly = 0)
    {
        if (plugin.Catalog is not { } catalog) return;
        var bundles = catalog.Bundles(item.Id).Where(b => questOnly == 0 || b.QuestId == questOnly).ToArray();
        if (bundles.Length == 0) return;
        var chosen = bundles.FirstOrDefault(b => b.Key == selectedBundle)
            ?? bundles.OrderBy(b => b.Kind == "Quest" ? snapshot.ActiveQuests?.ContainsKey(b.QuestId) == true ? 0 : b.Exact ? 1 : 3 : b.Kind == "Recipe" ? 4 : 2).First();
        Heading("Комплект для цели", "Requirement set");
        ImGui.SetNextItemWidth(-1);
        if (bundles.Length > 1 && ImGui.BeginCombo("###BundleChoice", BundleLabel(chosen)))
        {
            foreach (var candidate in bundles)
                if (ImGui.Selectable(BundleLabel(candidate) + "###" + candidate.Key, candidate.Key == chosen.Key))
                { chosen = candidate; selectedBundle = candidate.Key; }
            ImGui.EndCombo();
        }
        else if (bundles.Length == 1) ImGui.TextWrapped(BundleLabel(chosen));
        if (chosen.Kind == "Exchange") exchangeKey = chosen.Key["exchange:".Length..];
        if (chosen.Kind == "Research" && item.ResearchUses.FirstOrDefault(u => u.Costs is { Length: > 1 } && ItemCatalog.ResearchBundleKey(u) == chosen.Key) is { } linked)
            selectedResearch = $"{item.Id}:{linked.Kind}:{linked.Context}:{linked.RequiredCount}";
        chosen = catalog.ForCharacter(chosen, snapshot);
        ImGui.PushID(chosen.Key);
        var progress = BundlePolicy.Evaluate(chosen, snapshot);
        int complete = progress.Count(p => p.Missing == 0);
        ImGui.TextColored(complete == progress.Length && chosen.Exact ? Teal : Gold,
            T($"Собрано позиций: {complete}/{progress.Length}", $"Requirements collected: {complete}/{progress.Length}"));
        if (chosen.Kind == "Quest" && snapshot.QuestCompleted(chosen.QuestId) == true)
            Hint(T("Квест уже завершён. Показан состав комплекта для справки; другие применения проверяй отдельно.", "Quest already completed. This is a reference checklist; review other uses separately."));
        else if (BundlePolicy.Collected(chosen, snapshot)) Hint(T("Предметы собраны. Перед сдачей проверь этап, доступ и качество у NPC.", "Items collected. Confirm objective, access and quality with the NPC before handing in."));
        if (!chosen.Exact) Hint(T("Связанные требования: одновременная сдача или полная цена не подтверждены. Это список для проверки.", "Associated requirements: simultaneous hand-in or complete price is unconfirmed. This is a review checklist."));
        if (chosen.Kind == "Quest") Hint(T("Запас для указанной задачи. Уже сданные предметы не определяются по сумке — сверяй текущий этап в журнале.", "Stock for this objective. Already submitted items cannot be inferred from bags; check your current journal step."));
        if (chosen.Kind == "Recipe") Hint(T("Материалы на одно изготовление, без автоматического расчёта вложенных рецептов.", "Materials for one craft; nested recipes are not automatically expanded."));
        if (ImGui.CollapsingHeader(T("Состав и недостающее", "Contents and missing items") + "###BundleParts", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool showAll = progress.Length <= 8 || ImGui.CollapsingHeader(T($"Показать все позиции ({progress.Length}); сейчас первые 6", $"Show all {progress.Length} requirements; showing first 6") + "###AllBundleParts");
            if (ImGui.BeginTable("BundleItems", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn(T("Предмет / источник", "Item / source"), ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(T("Есть / нужно", "Held / need"), ImGuiTableColumnFlags.WidthFixed, 92 * S);
                ImGui.TableSetupColumn(T("Осталось", "Missing"), ImGuiTableColumnFlags.WidthFixed, 68 * S);
                ImGui.TableHeadersRow();
                int rowIndex = 0;
                foreach (var row in progress.OrderBy(p => p.Missing == 0).Take(showAll ? progress.Length : 6))
                {
                    ImGui.PushID(rowIndex++);
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    var fact = catalog.Find(row.Part.ItemId);
                    if (fact != null) { DrawIcon(fact, 22); ImGui.SameLine(); }
                    if (ImGui.SmallButton(row.Part.Name + "###BundleItem")) { databaseItem = row.Part.ItemId; requestedTab = 3; }
                    if (row.Part.Quality == "hq") Hint("HQ");
                    if (row.Part.Collectability > 0) Hint(T($"Ценность >= {row.Part.Collectability}", $"Collectability >= {row.Part.Collectability}"));
                    if (row.Equipped > 0) Hint(T("Есть надетый экземпляр: перед сдачей сними его.", "Includes an equipped copy: unequip before handing in."));
                    if (row.Missing != 0 && fact != null) Hint(BundleSourceSummary(fact));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{row.Eligible?.ToString() ?? "?"} / {row.Part.Count?.ToString() ?? "?"}");
                    if (row.Eligible.HasValue && row.Owned != row.Eligible.Value && ImGui.IsItemHovered())
                        ImGui.SetTooltip(T($"Всего {row.Owned}; засчитаны только экземпляры подходящего качества.", $"Total {row.Owned}; only eligible quality is counted."));
                    ImGui.TableNextColumn();
                    ImGui.TextColored(row.Missing == 0 ? Teal : Gold, row.Missing == 0 ? T("Готово", "Collected") : row.Missing?.ToString() ?? "?");
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
            Hint(T("Сумки + Armoury + надетое. Ретейнеры, Saddlebag и ключевые предметы не проверены. Запас общий для всех целей.", "Bags + Armoury + equipped. Retainers, saddlebags and key-item storage are unchecked. Stock is shared between goals."));
            if (progress.Any(p => p.Missing == null)) Hint(T("Знак ? означает неизвестное количество, качество, профессию или непрочитанный контейнер.", "? means unknown amount, quality, accepted job or unscanned storage."));
            foreach (var contact in (chosen.Contacts ?? []).Take(1)) DrawNpc(contact);
            if (chosen.QuestId != 0 && ImGui.SmallButton(T("Условия и этапы квеста", "Quest access and objectives") + "###BundleQuest")) { selectedQuest = chosen.QuestId; requestedTab = 4; }
            if (chosen.Wiki.Length > 0) Wiki(chosen.Wiki, T("Вики комплекта", "Set wiki"), "BundleWiki");
            if (ImGui.CollapsingHeader(T("Где получить недостающее — подробнее", "Where to get missing items — details") + "###MissingSources"))
                foreach (var row in progress.Where(p => p.Missing != 0).DistinctBy(p => p.Part.ItemId))
                    if (catalog.Find(row.Part.ItemId) is { } missing)
                    {
                        ImGui.PushID("missing:" + row.Part.ItemId);
                        ImGui.TextColored(Teal, missing.Name);
                        DrawAcquisition(missing, snapshot);
                        Wiki(missing.Name, T("Вики предмета", "Item wiki"), "MissingWiki");
                        ImGui.PopID();
                    }
        }
        ImGui.PopID();
    }
    private string BundleLabel(ItemBundle bundle) => BundleKind(bundle.Kind) + " · " + bundle.Name
        + (bundle.Kind == "Quest" && bundle.Parts.FirstOrDefault()?.Objective is int objective ? T($" · задача {objective + 1}", $" · objective {objective + 1}") : "");
    private string BundleKind(string kind) => kind switch { "Quest" => T("Квест", "Quest"), "Recipe" => T("Рецепт", "Recipe"), _ => T("Вариант обмена", "Exchange option") };
    private string BundleSourceSummary(ItemFacts item)
    {
        if (DemiatmaGuide.Find(item.Id) is { } guide && guide.Name == item.Name)
            return $"FATE: {guide.Zone} · South Horn: " + Region(guide.Region);
        if (item.BuyOffers.FirstOrDefault() is { } offer) return T("Купить: ", "Buy: ") + offer.Shop + $" · {offer.Price} gil";
        if (item.AcquisitionSources.FirstOrDefault() is { } source) return SourceType(source.Kind) + ": " + source.Name;
        if (item.AcquisitionTrades.FirstOrDefault() is { } trade) return T("Обмен: ", "Exchange: ") + trade.Shop;
        if (item.QuestUses.FirstOrDefault(q => q.Reward) is { } quest) return T("Награда: ", "Reward: ") + quest.Name;
        return T("Источник не подтверждён — вики по имени предмета.", "Source unconfirmed — open the item wiki.");
    }
    private string Region(string region) => T(region switch { "southeast" => "юго-восток", "southwest" => "юго-запад", "west" => "запад", "north" => "север", "east" => "восток", _ => "центр / северо-восток" }, region);
    private void DrawDemiatmaGuide(uint id, InventorySnapshot snapshot)
    {
        if (DemiatmaGuide.Find(id) is not { } guide || plugin.Catalog?.Find(id)?.Name != guide.Name) return;
        ImGui.TextWrapped($"FATE: {guide.Zone}. Occult Crescent: South Horn · " + Region(guide.Region));
        ImGui.TextWrapped(T("События South Horn: ", "South Horn events: ") + string.Join(", ", guide.Events));
        ImGui.TextColored(snapshot.ActiveQuests?.ContainsKey(70855) == true ? Teal : Gold,
            T("Условие выпадения: Arcane Artistry должен быть активен. Выпадение случайное.", "Drop condition: Arcane Artistry must be active. Drops are random."));
        if (snapshot.ActiveQuests?.ContainsKey(70855) != true) DrawQuestAccess(snapshot, 70855);
    }
}
