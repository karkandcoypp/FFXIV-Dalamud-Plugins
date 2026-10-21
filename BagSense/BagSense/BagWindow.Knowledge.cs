using System.Diagnostics;
using Dalamud.Bindings.ImGui;
namespace BagSense;

public sealed partial class BagWindow
{
    private void Hint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Muted); ImGui.TextWrapped(text); ImGui.PopStyleColor();
    }
    private void Wiki(string name, string label, string id)
    {
        ImGui.BeginDisabled(staticPreview);
        if (ImGui.SmallButton(label + "###Wiki" + id))
            Process.Start(new ProcessStartInfo("https://ffxiv.consolegameswiki.com/wiki/Special:Search?search=" + Uri.EscapeDataString(name) + "&go=Go") { UseShellExecute = true });
        ImGui.EndDisabled();
    }
    private void DrawIdentity(ItemFacts item)
    {
        Wiki(item.Name, T("Вики предмета", "Item wiki"), "Item" + item.Id);
        if (item.Description.Length > 0 && item.Reward?.Kind != "Mount")
            ImGui.TextWrapped(item.Description);
        if (item.ResearchUses.Length == 0 && item.Role is "Unresolved" or "" && item.QuestUses.All(q => q.Reward) && item.ExchangeUses.Length == 0 && item.DeliveryUses.Length == 0 && item.Unlocked == null)
            ImGui.TextWrapped(T("Покрытие: особое применение ещё не подтверждено. Наличие цены продажи или отсутствие рецептов не означает, что предмет лишний.",
                "Coverage: a special use is not yet verified. A vendor price or missing recipe does not mean this item is surplus."));
    }
    private void DrawQuestAccess(InventorySnapshot snapshot, uint id)
    {
        if (plugin.Catalog is not { } catalog || !catalog.QuestGates.TryGetValue(id, out var gate)) return;
        if (snapshot.ActiveQuests?.ContainsKey(id) == true)
            ImGui.TextColored(Teal, T("Принят: следуй текущему этапу в журнале.", "Accepted: follow the current journal objective."));
        else if (snapshot.QuestCompleted(id) == true)
            ImGui.TextColored(Teal, T("Квест завершён персонажем. Другие применения предмета проверь в карточке.", "Quest completed by this character. Review other item uses in the card."));
        else
        {
            var blockers = QuestAccessPolicy.Check(snapshot, gate);
            bool blocked = blockers.Any(b => b.Kind is "level" or "job" or "previous" or "onePrevious");
            ImGui.TextColored(Gold, blocked ? T("Пока недоступен на текущей профессии:", "Not currently accessible on this job:")
                : T("Квест не принят. Известные условия ниже; полная доступность проверяется у NPC.", "Not accepted. Known conditions below; confirm full availability with the NPC."));
            foreach (var b in blockers)
            {
                if (b.Kind == "level") ImGui.TextWrapped(T($"Нужен уровень {b.Required}; сейчас {b.Actual} на текущей профессии.", $"Requires level {b.Required}; current job level is {b.Actual}."));
                else if (b.Kind == "job") ImGui.TextWrapped(T("Нужна подходящая профессия: ", "Switch to an eligible job: ") + gate.JobLabel);
                else if (b.Kind is "previous" or "onePrevious" or "previousUnknown")
                {
                    ImGui.TextWrapped((b.Kind == "onePrevious" ? T("Достаточно одного из: ", "Complete one of: ") : b.Kind == "previousUnknown" ? T("Статус предыдущего квеста неизвестен: ", "Prerequisite status unknown: ") : T("Сначала заверши: ", "Complete first: ")) + b.Name);
                    Wiki(b.Name, T("Вики условия", "Prerequisite wiki"), "Previous" + b.Id);
                    ImGui.SameLine();
                    if (ImGui.SmallButton(T("Показать цепочку", "Show quest chain") + "###QuestChain" + b.Id))
                    { selectedQuest = b.Id; requestedTab = 4; }
                }
                else if (b.Kind == "characterUnknown") ImGui.TextWrapped(T("Уровень и профессия персонажа пока не прочитаны.", "Character level and job are not available yet."));
                else if (b.Kind == "additional") ImGui.TextWrapped(T("Дополнительное условие не проверено: ", "Additional condition not verified: ") + ExtraCondition(b.Name));
            }
            if (gate.Level > 0) Hint($"Lv {gate.Level} · {gate.JobLabel}");
        }
        Wiki(gate.Name, T("Вики квеста", "Quest wiki"), "Quest" + gate.Id);
    }
    private string ExtraCondition(string kind) => kind switch
    {
        "questlock" => T("несовместимые квесты или выбранная ветка", "incompatible quests or selected branch"),
        "instance" => T("прохождение требуемого контента", "required duty progress"),
        "secondjob" => T("уровень второй профессии", "secondary job level"),
        "company" => T("Grand Company и ранг", "Grand Company and rank"),
        "tribe" => T("репутация племени", "tribal reputation"),
        "satisfaction" => T("уровень отношений с NPC сдач", "custom delivery satisfaction rank"),
        "housing" => T("владение жильём", "housing ownership"),
        "mount" => T("требуемый маунт", "required mount"),
        "jobunlock" => T("открытие требуемой профессии", "required job unlock"),
        "time" => T("время, событие или лимит повторения", "time, event or repeat limit"),
        "levelmax" => T("ограничение максимального уровня", "maximum level restriction"),
        _ => T("специальное условие игрового сценария", "special game script condition")
    };
    private void DrawAcquisition(ItemFacts item, InventorySnapshot snapshot)
    {
        Heading("Как получить / получить снова", "How to obtain / replace");
        bool found = item.BuyOffers.Length > 0 || item.AcquisitionSources.Length > 0 || item.AcquisitionTrades.Length > 0 || item.QuestUses.Any(q => q.Reward);
        if (!found) ImGui.TextWrapped(T("Подтверждённый источник ещё не найден в подключённых таблицах. Это не значит, что предмет недоступен или лишний; открой вики предмета.",
            "No verified source was found in the indexed tables. This does not mean the item is unobtainable or surplus; open its wiki."));
        bool allMerchants = item.BuyOffers.Length <= 3 || ImGui.CollapsingHeader(T($"Все торговцы ({item.BuyOffers.Length})", $"All merchants ({item.BuyOffers.Length})") + "###AllMerchants");
        foreach (var offer in item.BuyOffers.Take(allMerchants ? item.BuyOffers.Length : 3))
        {
            ImGui.TextWrapped(T("Купить: ", "Buy: ") + offer.Shop + $" · {offer.Price} gil" + (offer.Conditional ? T(" · есть условия", " · conditional") : ""));
            if (offer.Destinations.FirstOrDefault() is { } route) DrawNpc(route.Location);
        }
        var sourceRows = item.AcquisitionSources.Distinct().ToArray();
        bool all = sourceRows.Length <= 6 || ImGui.CollapsingHeader(T($"Все источники ({sourceRows.Length})", $"All sources ({sourceRows.Length})") + "###AllSources");
        foreach (var source in sourceRows.Take(all ? sourceRows.Length : 3))
        {
            ImGui.PushID("source:" + source.Kind + source.Id + source.Name);
            ImGui.TextWrapped(SourceType(source.Kind) + ": " + source.Name + (source.Detail == "Unresolved currency encoding" ? T(" · формат валюты ещё не расшифрован; проверь стоимость у NPC", " · currency format unresolved; confirm cost with the NPC") : source.Detail.Length > 0 ? " · " + source.Detail : ""));
            if (source.Kind == "Craft" && plugin.Catalog?.RecipeIngredients.TryGetValue(source.Id, out var ingredients) == true
                && ImGui.CollapsingHeader(T("Материалы рецепта", "Recipe materials") + "###Ingredients"))
                foreach (var ingredient in ingredients) ImGui.TextWrapped($"{ingredient.Count} × {ingredient.Name}");
            ImGui.PopID();
        }
        if (item.AcquisitionTrades.Length > 0 && ImGui.CollapsingHeader(T($"Получение обменом ({item.AcquisitionTrades.Length})", $"Obtain through exchanges ({item.AcquisitionTrades.Length})") + "###AcquireTrades"))
            foreach (var offer in item.AcquisitionTrades)
            {
                ImGui.PushID("obtain:" + offer.Key);
                ImGui.TextWrapped(offer.Shop + $" → ×{offer.RewardCount} {item.Name}");
                foreach (var cost in offer.FullCosts) ImGui.TextWrapped($"{cost.Count} × {cost.Name}");
                if (offer.Destinations.FirstOrDefault() is { } route) { DrawRequirements(DestinationPolicy.Requirements(offer, route), snapshot); DrawNpc(route.Location); }
                else
                {
                    DrawRequirements(offer.Conditions, snapshot);
                    ImGui.TextWrapped(T("NPC этого обмена ещё не сопоставлен. Проверь вики перед поездкой.", "This exchange has no confirmed NPC mapping yet. Check the wiki before travelling."));
                }
                ImGui.PopID();
            }
        if (item.QuestUses.Any(q => q.Reward) && ImGui.CollapsingHeader(T("Квесты, выдающие этот предмет", "Quests awarding this item") + "###AcquireQuests")) DrawQuestUses(item, snapshot, true);
        if (item.CraftingLinks.Length > 0 && ImGui.CollapsingHeader(T($"Что сделать из предмета ({item.CraftingLinks.Length} рецептов)", $"What to craft with it ({item.CraftingLinks.Length} recipes)") + "###CraftLinks"))
            foreach (var recipe in item.CraftingLinks)
            {
                ImGui.PushID((int)recipe.RecipeId);
                ImGui.TextWrapped($"{recipe.Required} × {item.Name} → {recipe.Yield} × {recipe.Result} · {recipe.Job} Lv {recipe.Level}");
                if (ImGui.SmallButton(T("Открыть результат", "Open result") + "###RecipeResult")) { databaseItem = recipe.ResultId; requestedTab = 3; }
                ImGui.PopID();
            }
    }
    private string SourceType(string kind) => kind switch
    {
        "Craft" => T("Создать", "Craft"), "Gather" => T("Собрать", "Gather"), "Fish" => T("Рыбалка", "Fishing"),
        "Spear" => T("Подводная охота", "Spearfishing"), "Drop" => T("Может выпасть с противника", "Possible enemy drop"),
        "Duty" => T("Возможная награда контента", "Possible duty reward"), "Container" => T("Возможная награда контейнера", "Possible container reward"),
        "Contains" => T("Может содержать", "May contain"), "Fate" => "FATE / Critical Encounter", _ => T("Магазин обмена", "Exchange shop")
    };
}
