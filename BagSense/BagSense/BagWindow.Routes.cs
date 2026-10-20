using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BagSense;

public sealed partial class BagWindow
{
    private string exchangeKey = "";
    private string destinationKey = "";
    private string routeKey = "";

    private void Heading(string ru, string en)
    {
        ImGui.Spacing();
        ImGui.TextColored(Teal, T(ru, en));
    }

    private void DrawItem(InventorySnapshot snapshot, CharacterData data, Advice advice)
    {
        var item = snapshot.Items.GetValueOrDefault(advice.ItemId);
        if (item == null) { ImGui.TextWrapped(L.Reason(Reason.UnknownItem)); return; }
        DrawIcon(item, 44);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(item.Name);
        ImGui.TextDisabled(item.Category);
        ImGui.EndGroup();
        DrawDecision(item, snapshot, advice);
        Heading("Назначение", "Purpose");
        ImGui.TextWrapped(L.Purpose(item));
        DrawIdentity(item);
        Hint(T($"В наличии {advice.Total}   /   Сохранить {Math.Min(advice.Total, advice.CanPlan ? advice.Reserved : advice.Total)}   /   В план { (advice.CanPlan ? advice.Available : 0)}",
            $"Owned {advice.Total}   /   Keep {Math.Min(advice.Total, advice.CanPlan ? advice.Reserved : advice.Total)}   /   Plannable {(advice.CanPlan ? advice.Available : 0)}"));
        int questReserve = QuestPolicy.Required(snapshot, item.Id);
        if (questReserve > 0) ImGui.TextColored(Gold, T($"Защита квестов: оставить {questReserve} шт. Продажа этого запаса исключена из плана.",
            $"Quest protection: keep {questReserve} items. This stock is excluded from sale plans."));
        if (!advice.CanPlan && advice.Reason != Reason.QuestItem) ImGui.TextWrapped(L.Reason(advice.Reason));
        if (snapshot.Gearsets.TryGetValue(item.Id, out var gearsets)) ImGui.TextWrapped(T("В комплекте: ", "Gearsets: ") + string.Join(", ", gearsets));
        ImGui.Separator();

        DrawDeliveries(item, snapshot);
        if (item.ExchangeUses.Length > 0 && action == PlanAction.ExchangeReview) DrawExchange(item, snapshot);
        else if (action == PlanAction.Vendor) DrawSale(item, snapshot);
        else if (item.QuestUses.Any(q => !q.Reward)) DrawQuestUses(item, snapshot, rewards: false);
        else if (action == PlanAction.Use) ImGui.TextWrapped(T("Используй предмет прямо из сумки. Идти к NPC не требуется.", "Use this item from your inventory. No NPC visit is needed."));
        else if (action == PlanAction.Desynthesize) ImGui.TextWrapped(T("Разбор доступен в контекстном меню предмета. Перед разбором проверь, нужен ли внешний вид.",
            "Use Desynthesis in the item's context menu. Check whether you want its appearance first."));

        DrawAcquisition(item, snapshot);
        Heading("Следующее действие", "Next action");
        DrawPlanControls(snapshot, data, advice);
        if (reviewMode)
        {
            if (ImGui.SmallButton(T("Позже — следующий предмет", "Later — next item") + "###Next"))
            { data.Edit(item.Id).DeferUntil = DateTimeOffset.UtcNow.AddHours(1); plugin.Changed(); selected = 0; }
        }
        ImGui.Separator();
        if (ImGui.CollapsingHeader(T("Оставить для себя / для цели", "Keep for myself / a goal") + "###KeepSettings"))
        {
            ImGui.SetNextItemWidth(-1);
            if (InputHint("###Note", T("Зачем я храню этот предмет", "Why I keep this item"), ref note, 1024))
            { data.Edit(item.Id).Note = note; plugin.Changed(); }
            bool keep = data.Decision(item.Id).AlwaysKeep;
            if (ImGui.Checkbox(T("Всегда сохранять", "Always keep") + "###Keep", ref keep))
            { data.Edit(item.Id).AlwaysKeep = keep; plugin.Changed(); }
            ImGui.SetNextItemWidth(110 * S);
            if (Number(T("Минимальный запас", "Minimum stock") + "###Reserve", ref keepQuantity))
            { keepQuantity = Math.Clamp(keepQuantity, 0, 999999); data.Edit(item.Id).KeepQuantity = keepQuantity; plugin.Changed(); }
            DrawGoalAssignment(data, item.Id);
            if (ImGui.SmallButton(T("Отложить решение на день", "Defer for one day") + "###Defer"))
            { data.Edit(item.Id).DeferUntil = DateTimeOffset.UtcNow.AddDays(1); plugin.Changed(); }
            ImGui.SameLine();
            if (ImGui.SmallButton(T("Вернуть в разбор", "Review again") + "###Undefer"))
            { data.Edit(item.Id).DeferUntil = null; plugin.Changed(); }
        }
        if (item.ExchangeUses.Length > 0 && action != PlanAction.ExchangeReview && ImGui.CollapsingHeader(T("Другие применения: обмены", "Other uses: exchanges") + "###OtherExchanges")) DrawExchange(item, snapshot);
        if (item.QuestUses.Any(q => !q.Reward) && action != PlanAction.ManualReview
            && ImGui.CollapsingHeader(T("Материал для квестов", "Requested by quests") + "###QuestUses")) DrawQuestUses(item, snapshot, false);
        if (item.QuestUses.Any(q => q.Reward) && ImGui.CollapsingHeader(T("Получение в награду за квест", "Obtained as a quest reward") + "###QuestRewards")) DrawQuestUses(item, snapshot, true);
        if (ImGui.CollapsingHeader(T("Описание, рецепты и источники", "Description, recipes and sources") + "###More"))
        {
            if (item.Description.Length > 0) ImGui.TextWrapped(item.Description);
            if (item.RecipeUses.Length > 0)
            {
                Heading("Используется в рецептах", "Used in recipes");
                foreach (string recipe in item.RecipeUses) ImGui.TextWrapped("• " + recipe);
            }
            foreach (var offer in item.BuyOffers.Take(4))
            {
                ImGui.PushID($"{offer.ShopId}:{offer.Price}:{offer.Conditional}");
                ImGui.TextWrapped(T($"Купить заново: {offer.Shop} — {offer.Price} gil", $"Buy again: {offer.Shop} — {offer.Price} gil"));
                var location = offer.Destinations.FirstOrDefault()?.Location;
                if (location != null) DrawNpc(location);
                if (offer.Conditional) ImGui.TextDisabled(T("Доступ к магазину имеет условия.", "This shop has access conditions."));
                ImGui.PopID();
            }
            ImGui.TextDisabled($"ID {item.Id}");
            if (!plugin.Demo && ImGui.SmallButton(T("Подробнее в Teamcraft", "More on Teamcraft") + "###Teamcraft"))
                Process.Start(new ProcessStartInfo($"https://ffxivteamcraft.com/db/en/item/{item.Id}") { UseShellExecute = true });
        }
    }

    private void DrawExchange(ItemFacts item, InventorySnapshot snapshot)
    {
        var offers = item.ExchangeUses.OrderBy(e => RewardPolicy.Unlocked(snapshot, e.RewardId) == true)
            .ThenBy(e => plugin.Catalog?.Find(e.RewardId)?.Reward?.Kind switch { "Mount" => 0, "Minion" => 1, "Music" => 2, _ => 3 })
            .ThenByDescending(e => e.Destinations.Any(r => r.Location.CanMap)).ThenBy(e => e.Cost).ToArray();
        var offer = offers.FirstOrDefault(e => e.Key == exchangeKey) ?? offers[0];
        exchangeKey = offer.Key;
        Heading("Что получить за этот предмет", "What you can exchange it for");
        string[] rewardFilters = [T("Все награды", "All rewards"), T("Маунты", "Mounts"), T("Миньоны", "Minions"), T("Музыка", "Music"), T("Экипировка", "Equipment")];
        ImGui.SetNextItemWidth(210 * S);
        ImGui.Combo("###RewardFilter", ref rewardFilter, rewardFilters, rewardFilters.Length);
        string[] kinds = ["", "Mount", "Minion", "Music", "Equipment"];
        var filtered = offers.Where(e => rewardFilter == 0 || plugin.Catalog?.Find(e.RewardId)?.Reward?.Kind == kinds[rewardFilter]).ToArray();
        if (filtered.Length == 0) ImGui.TextWrapped(T("Наград выбранного типа нет. Показаны остальные варианты.", "No rewards of this type. Showing other options."));
        else offers = filtered;
        offer = offers.FirstOrDefault(e => e.Key == exchangeKey) ?? offers[0]; exchangeKey = offer.Key;
        if (offers.Length > 1)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("###ExchangeReward", offer.Reward))
            {
                foreach (var candidate in offers)
                {
                    var reward = plugin.Catalog?.Find(candidate.RewardId);
                    if (reward != null) { DrawIcon(reward, 28); ImGui.SameLine(); }
                    if (ImGui.Selectable($"{candidate.Reward} · {RewardType(reward?.Reward?.Kind ?? "Item")}###" + candidate.Key, candidate.Key == exchangeKey))
                    { exchangeKey = candidate.Key; destinationKey = ""; }
                }
                ImGui.EndCombo();
            }
            offer = offers.First(e => e.Key == exchangeKey);
        }
        else ImGui.TextWrapped(offer.Reward);
        if (plugin.Catalog?.Find(offer.RewardId) is { } selectedReward) DrawRewardCard(selectedReward, offer.RewardCount, snapshot);
        else ImGui.TextWrapped(T($"Получишь: {offer.RewardCount} × {offer.Reward}", $"You receive: {offer.RewardCount} × {offer.Reward}"));
        Heading("Что отдать за награду", "What you pay");
        foreach (var cost in offer.FullCosts)
        {
            int held = snapshot.Quantity(cost.ItemId);
            string quantity = cost.ItemId < 100 ? T("не проверено", "not checked") : held.ToString();
            string missing = cost.ItemId >= 100 && held < cost.Count ? T($" · не хватает {cost.Count - held}", $" · need {cost.Count - held} more") : "";
            ImGui.TextColored(cost.ItemId >= 100 && held < cost.Count ? Gold : Muted, T($"{cost.Name}: нужно {cost.Count}, есть {quantity}",
                $"{cost.Name}: required {cost.Count}, owned {quantity}") + missing);
            if (cost.Collectability > 0) ImGui.TextWrapped(T($"Требуемая коллекционная ценность: {cost.Collectability}", $"Required collectability: {cost.Collectability}"));
        }
        var affordable = DestinationPolicy.Affordable(offer, snapshot);
        if (affordable != null) ImGui.TextDisabled(T($"По количеству в сумках: до {affordable} обменов. Проверь качество и доступ.",
            $"Bag quantities support up to {affordable} exchanges. Check item quality and access."));
        Heading("Куда идти", "Where to go");
        var routes = offer.Destinations.Where(r => r.Location.CanMap).OrderByDescending(r => r.Location.Territory == snapshot.TerritoryId).ThenBy(r => r.Requirements.Length)
            .DistinctBy(r => r.Location.Key).ToArray();
        if (routes.Length == 0)
        {
            routeKey = ""; destinationKey = "";
            ImGui.TextWrapped(T("NPC и точка на карте для этого обмена пока не подтверждены.", "The NPC and map location for this exchange are not confirmed yet."));
            if (offer.Shop.Length > 0) ImGui.TextDisabled(offer.Shop);
            DrawRequirements(offer.Conditions, snapshot);
            return;
        }
        var route = routes.FirstOrDefault(r => r.Location.Key == destinationKey) ?? routes[0];
        destinationKey = route.Location.Key;
        if (routes.Length > 1)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("###ExchangeNpc", route.Location.Npc + " · " + route.Location.Zone))
            {
                foreach (var candidate in routes)
                    if (ImGui.Selectable(candidate.Location.Npc + " · " + candidate.Location.Zone + "###" + candidate.Location.Key,
                        candidate.Location.Key == destinationKey)) destinationKey = candidate.Location.Key;
                ImGui.EndCombo();
            }
            route = routes.First(r => r.Location.Key == destinationKey);
        }
        DrawNpc(route.Location);
        routeKey = DestinationPolicy.Key(route);
        string menu = string.IsNullOrWhiteSpace(route.Menu) ? offer.Shop : route.Menu;
        if (!string.IsNullOrWhiteSpace(menu)) ImGui.TextWrapped(T("Меню NPC: ", "NPC menu: ") + menu);
        DrawRequirements(DestinationPolicy.Requirements(offer, route), snapshot);
    }

    private void DrawSale(ItemFacts item, InventorySnapshot snapshot)
    {
        Heading("Кому продать", "Where to sell");
        ImGui.TextWrapped(T($"Обычная продажа за gil. Цена NQ: {item.SellPrice} за штуку. Подойдёт обычный торговец с вкладкой продажи.",
            $"Ordinary gil sale. NQ vendor price: {item.SellPrice} each. Use a regular merchant's selling tab."));
        var merchants = item.SaleLocations.Where(l => l.CanMap).OrderByDescending(l => l.Territory == snapshot.TerritoryId)
            .ThenByDescending(l => l.Npc is "Bango Zango" or "Roarich" or "Maisenta").ToArray();
        if (merchants.Length == 0) { ImGui.TextDisabled(T("Местоположение обычного торговца не определено.", "No regular merchant location was found.")); return; }
        var merchant = merchants.FirstOrDefault(l => l.Key == destinationKey) ?? merchants[0];
        destinationKey = merchant.Key;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("###SaleNpc", merchant.Npc + " · " + merchant.Zone))
        {
            foreach (var candidate in merchants.Take(50).Prepend(merchant).DistinctBy(l => l.Key))
                if (ImGui.Selectable(candidate.Npc + " · " + candidate.Zone + "###" + candidate.Key, candidate.Key == destinationKey)) destinationKey = candidate.Key;
            ImGui.EndCombo();
        }
        DrawNpc(merchants.First(l => l.Key == destinationKey));
    }

    private void DrawNpc(NpcLocation location)
    {
        ImGui.TextWrapped(location.Npc);
        ImGui.TextColored(Muted, $"{location.Zone}  ·  X: {location.X:0.0}  Y: {location.Y:0.0}");
        ImGui.BeginDisabled(!location.CanMap || plugin.Demo || staticPreview);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.10f, .31f, .42f, 1));
        if (ImGui.Button(T("Открыть карту", "Open map") + "###Map" + location.Key)) plugin.OpenMap(location);
        ImGui.PopStyleColor();
        ImGui.EndDisabled();
        if (plugin.MapFailed) ImGui.TextWrapped(T("Карта не открылась. Попробуй ещё раз после закрытия других окон.", "The map did not open. Close other windows and try again."));
    }

    private void DrawRequirements(IEnumerable<RouteRequirement> requirements, InventorySnapshot snapshot)
    {
        bool unknown = false;
        foreach (var requirement in requirements.Distinct())
        {
            if (requirement.Kind == "quest")
            {
                var completed = snapshot.QuestCompleted(requirement.Id);
                string status = completed == true ? T("выполнен", "completed") : completed == false ? T("не завершён", "not completed") : T("статус неизвестен", "status unknown");
                ImGui.TextWrapped(T("Квест для доступа: ", "Access quest: ") + requirement.Name + " — " + status);
            }
            else if (requirement.Kind == "job") ImGui.TextWrapped(T("Профессии: ", "Jobs: ") + requirement.Name);
            else if (requirement.Kind == "achievement") ImGui.TextWrapped(T("Требуется достижение: ", "Required achievement: ") + requirement.Name);
            else if (requirement.Kind == "duty") ImGui.TextWrapped(T("Условие контента: ", "Duty requirement: ") + requirement.Name);
            else unknown = true;
        }
        if (unknown) ImGui.TextDisabled(T("Дополнительные условия проверяются в диалоге NPC.", "Check additional conditions in the NPC dialogue."));
    }

    private void DrawQuestUses(ItemFacts item, InventorySnapshot snapshot, bool rewards)
    {
        foreach (var quest in item.QuestUses.Where(q => q.Reward == rewards).OrderByDescending(q => snapshot.ActiveQuests?.ContainsKey(q.QuestId) == true).ThenBy(q => snapshot.QuestCompleted(q.QuestId) == true))
        {
            ImGui.PushID((int)quest.QuestId);
            ImGui.TextWrapped(quest.Name);
            DrawQuestAccess(snapshot, quest.QuestId);
            bool? completed = snapshot.QuestCompleted(quest.QuestId);
            ImGui.TextWrapped(rewards ? T("Выдаётся в награду, сдавать предмет не требуется.", "A quest reward, not an item to hand in.")
                : quest.Objective.Length > 0 ? T($"Этап {(quest.TodoIndex ?? 0) + 1}: ", $"Objective {(quest.TodoIndex ?? 0) + 1}: ") + quest.Objective
                : T("Запрашивается в ходе квеста. Точный этап не подтверждён — проверь журнал.", "Requested during this quest. Exact objective is unconfirmed — check your journal."));
            if (!rewards)
            {
                foreach (string variant in quest.Variants ?? []) ImGui.TextWrapped(T("Вариант по профессии: ", "Job variant: ") + variant);
                bool active = snapshot.ActiveQuests?.ContainsKey(quest.QuestId) == true;
                if (active && quest.RequirementVariants is { Length: > 0 } && QuestPolicy.Amount(snapshot, quest) == 0)
                    ImGui.TextColored(Muted, T("Этот предмет нужен другой профессии. Для принятого варианта резерв не создаётся.",
                        "This item is for a different job. No reserve is needed for your accepted variant."));
                if (active)
                {
                    bool final = snapshot.ActiveQuests![quest.QuestId] == 255 && quest.CompleteSequence == 255;
                    ImGui.TextColored(Teal, final ? T("Квест активен · финальный этап сдачи", "Active quest · final hand-in stage")
                        : T("Квест активен · предмет защищён до завершения", "Active quest · item protected until completion"));
                }
                else if (QuestPolicy.Relevant(snapshot, quest)) ImGui.TextWrapped(T("Нужен для ещё не завершённого общего этапа. Запас защищён даже до принятия квеста.", "Needed for an unfinished shared unlock. Stock is protected even before accepting the quest."));
                else if (snapshot.ActiveQuests != null) ImGui.TextDisabled(T("Сейчас не активен — автоматический резерв не создаётся.", "Not active — no automatic reservation."));
                else ImGui.TextWrapped(T("Журнал недоступен: возможный квестовый запас сохранён целиком.", "Journal unavailable: all potentially needed stock is protected."));
                int? amount = active && quest.RequirementVariants is { Length: > 0 } && QuestPolicy.Amount(snapshot, quest).HasValue
                    ? QuestPolicy.Amount(snapshot, quest) : quest.RequiredCount;
                ImGui.TextWrapped(amount.HasValue
                    ? T($"Требуется: {amount} шт.", $"Required: {amount} items.") + (quest.Quality == "hq" ? " HQ" : "")
                    : T("Количество не подтверждено: для активного квеста сохраняется весь запас.", "Quantity unconfirmed: all stock is kept for an active quest."));
                if (quest.Quality == "hq") ImGui.TextWrapped(T("Нужен HQ. В целях защиты качества сохраняются все экземпляры этого предмета.", "HQ required. All copies are protected to preserve item quality."));
                if (quest.Evidence.Length > 0 && ImGui.CollapsingHeader(T("Источник требования", "Requirement evidence") + "###Evidence"))
                    ImGui.TextWrapped(quest.Evidence + " · ffxiv-datamining / " + quest.Source);
            }
            ImGui.TextDisabled((completed == true ? T("Квест завершён", "Quest completed") : completed == false ? T("Квест ещё не завершён", "Quest not completed")
                : T("Статус квеста неизвестен", "Quest status unknown")) + (quest.Repeatable ? T(" · повторяемый", " · repeatable") : ""));
            var contact = quest.Contacts.FirstOrDefault();
            if (contact != null)
            {
                ImGui.TextDisabled(quest.Objective.Length > 0 && !quest.IssuerContact ? T("NPC для этого этапа:", "NPC for this objective:")
                    : quest.IssuerContact ? T("Где взять квест (место сдачи не подтверждено):", "Quest giver (hand-in location unconfirmed):") : T("Контакт завершения квеста:", "Quest completion contact:"));
                DrawNpc(contact);
            }
            else if (!rewards) ImGui.TextWrapped(T("NPC для сдачи пока не подтверждён. Место не подставляется из другого этапа.",
                "Hand-in NPC is unconfirmed. A different objective's location is not substituted."));
            ImGui.PopID();
        }
    }

    private void DrawPlanDestination(InventorySnapshot snapshot, ItemFacts item, PlanEntry entry)
    {
        if (entry.Action is not (PlanAction.Vendor or PlanAction.ExchangeReview)) return;
        var destination = DestinationPolicy.ForPlan(item, entry, snapshot.TerritoryId);
        if (entry.Action == PlanAction.ExchangeReview)
        {
            var exchange = item.ExchangeUses.FirstOrDefault(e => e.Key == entry.ExchangeKey) ?? (entry.ExchangeKey.Length == 0 ? item.ExchangeUses.FirstOrDefault() : null);
            if (exchange != null)
            {
                ImGui.TextWrapped(T("Цель обмена: ", "Exchange target: ") + exchange.Reward);
                if (plugin.Catalog?.Find(exchange.RewardId) is { } reward)
                {
                    DrawIcon(reward, 32); ImGui.SameLine();
                    ImGui.TextWrapped(RewardType(reward.Reward?.Kind ?? "Item") + " · " + (reward.Reward?.Name ?? reward.Name));
                }
                ImGui.TextWrapped(T("Полная цена: ", "Full cost: ") + string.Join(" + ", exchange.FullCosts.Select(c => $"{c.Count} × {c.Name}")));
                var affordable = DestinationPolicy.Affordable(exchange, snapshot);
                ImGui.TextColored(affordable == 0 ? Gold : Muted, affordable.HasValue
                    ? T($"По запасам в сумках: до {affordable} обменов.", $"From bag quantities: up to {affordable} exchanges.")
                    : T("Доступное число обменов не подтверждено — проверь все материалы у NPC.", "Exchange count is unconfirmed — check all materials at the NPC."));
                var route = exchange.Destinations.FirstOrDefault(r => r.Location.Key == destination?.Key
                    && (entry.RouteKey.Length == 0 || DestinationPolicy.Key(r) == entry.RouteKey));
                DrawRequirements(route == null ? exchange.Conditions : DestinationPolicy.Requirements(exchange, route), snapshot);
            }
        }
        if (destination != null) DrawNpc(destination);
        else ImGui.TextWrapped(T("Точка назначения не подтверждена. Открой карточку предмета и выбери NPC.",
            "Destination is not confirmed. Open the item card and choose an NPC."));
    }
}
