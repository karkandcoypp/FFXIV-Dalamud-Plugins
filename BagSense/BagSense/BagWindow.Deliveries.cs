using Dalamud.Bindings.ImGui;
namespace BagSense;
public sealed partial class BagWindow
{
    private string deliveryKey = "";
    private void DrawDecision(ItemFacts item, InventorySnapshot snapshot, Advice advice)
    {
        bool? learned = RewardPolicy.Unlocked(snapshot, item.Id);
        Heading("РЕШЕНИЕ ПО ПРЕДМЕТУ", "ITEM DECISION");
        if (!advice.CanPlan || advice.Available == 0)
            ImGui.TextColored(Gold, T("СОХРАНИТЬ · предмет защищён", "KEEP · protected item"));
        else if (QuestPolicy.SharedUnlockFinished(snapshot, item.Id))
        {
            ImGui.TextColored(Teal, T($"ОБЩИЙ ЭТАП ЗАВЕРШЁН · можно убрать до {advice.Available} шт.", $"SHARED UNLOCK COMPLETE · up to {advice.Available} can be removed"));
            ImGui.TextWrapped(T("Этот одноразовый запас для открытия Phantom Weapons больше не требуется. Личные резервы исключены из указанного количества.",
                "This one-time Phantom Weapon unlock stock is no longer required. Personal reserves are excluded from the shown quantity."));
        }
        else if (learned == false)
            ImGui.TextColored(Teal, T("СНАЧАЛА ИСПОЛЬЗОВАТЬ · ещё не получено", "USE FIRST · not yet collected"));
        else if (learned == true)
        {
            if (RewardPolicy.RedundantDuplicate(snapshot, advice))
                ImGui.TextColored(Teal, T($"МОЖНО УБРАТЬ ДУБЛИКАТ · до {advice.Available} шт.", $"DUPLICATE CAN BE REMOVED · up to {advice.Available}"));
            ImGui.TextColored(Teal, T("УЖЕ ПОЛУЧЕНО · повторное изучение не нужно", "ALREADY COLLECTED · no need to learn again"));
            ImGui.TextWrapped(T("Дубликат можно рассмотреть для продажи. Проверь сдачи, обмены и личный запас ниже перед удалением.",
                "Consider selling this duplicate. Review deliveries, exchanges and personal reserves below before discarding."));
        }
        else ImGui.TextColored(Gold, T("ПРОВЕРИТЬ ПРИМЕНЕНИЯ · удаление пока не подтверждено", "REVIEW USES · disposal is not confirmed"));
        if (item.Reward is { } reward && reward.CollectionId != 0)
            DrawRewardCard(item, (uint)advice.Total, snapshot, acquisition: false);
        else if (item.Reward is { Kind: "Minion" or "Music" or "Card" or "Emote" or "Hairstyle" })
            ImGui.TextWrapped(learned == true ? T("Коллекция: получено", "Collection: owned") : learned == false
                ? T("Коллекция: ещё не получено", "Collection: missing") : T("Коллекция: статус неизвестен", "Collection: unknown"));
    }

    private void DrawDeliveries(ItemFacts item, InventorySnapshot snapshot)
    {
        if (item.DeliveryUses.Length == 0) return;
        Heading("СДАЧИ И LEVE", "DELIVERIES & LEVES");
        string Kind(DeliveryUse d) => d.Kind switch { "Leve" => "Leve", "CustomDelivery" => T("Персональные сдачи", "Custom Delivery"), _ => T("Коллекционные предметы", "Collectables") };
        var choices = item.DeliveryUses.DistinctBy(d => (d.Kind, d.Id, d.Count, d.Collectability, d.Reward)).OrderByDescending(d => snapshot.ActiveLeves?.ContainsKey(d.Id) == true).ToArray();
        string Key(DeliveryUse d) => $"{item.Id}:{d.Kind}:{d.Id}:{d.Collectability}";
        var chosen = choices.FirstOrDefault(d => Key(d) == deliveryKey) ?? choices[0];
        ImGui.SetNextItemWidth(-1);
        if (choices.Length > 1 && ImGui.BeginCombo("###DeliveryChoice", chosen.Name + " · " + Kind(chosen)))
        {
            foreach (var option in choices)
                if (ImGui.Selectable(option.Name + " · " + Kind(option) + "###" + Key(option), Key(option) == Key(chosen)))
                { chosen = option; deliveryKey = Key(option); }
            ImGui.EndCombo();
        }
        foreach (var delivery in new[] { chosen })
        {
            ImGui.PushID($"delivery:{delivery.Kind}:{delivery.Id}");
            ImGui.Separator();
            bool active = delivery.Kind == "Leve" && snapshot.ActiveLeves?.ContainsKey(delivery.Id) == true;
            ImGui.TextWrapped((active ? T("ПРИНЯТО · ", "ACCEPTED · ") : "") + delivery.Name);
            if (delivery.Requirements.Length > 0) ImGui.TextWrapped(T("Условия: ", "Requirements: ") + delivery.Requirements);
            ImGui.TextWrapped(T($"На одну сдачу: {delivery.Count} шт.", $"Per delivery: {delivery.Count} items"));
            ImGui.TextWrapped(delivery.Quality == "collectable"
                ? T($"Коллекционная ценность: от {delivery.Collectability}. Не обычный NQ/HQ предмет.", $"Collectability: at least {delivery.Collectability}. Requires a collectable, not a normal NQ/HQ item.")
                : T("Качество: NQ подходит. Бонус HQ, если доступен, указан в игре.", "Quality: NQ accepted. See the game for any available HQ bonus."));
            int eligible = DeliveryPolicy.EligibleQuantity(snapshot, item.Id, delivery);
            ImGui.TextColored(eligible >= delivery.Count ? Teal : Gold,
                T($"Подходящего качества в сумках: {eligible} / {delivery.Count} на одну сдачу", $"Matching quality in bags: {eligible} / {delivery.Count} per delivery"));
            if (delivery.Repeats > 0) ImGui.TextWrapped(T($"Дополнительно до {delivery.Repeats} повторных сдач. Запас на полный цикл: {delivery.Count * (1 + delivery.Repeats)}.",
                $"Up to {delivery.Repeats} repeat deliveries. Full-cycle reserve: {delivery.Count * (1 + delivery.Repeats)}."));
            if (delivery.Kind == "CustomDelivery") ImGui.TextWrapped(T("Custom Delivery: награда растёт с ценностью. Вариант scrips зависит от профессии и уровня. Недельный запрос и доступные попытки проверь у NPC.",
                "Custom Delivery: rewards increase with collectability. Scrip variants depend on job and level. Confirm this week's request and allowances with the NPC."));
            if (delivery.Reward.Length > 0) ImGui.TextWrapped(T(delivery.Kind == "Collectable" ? "Ценность → scrips: " : "Базовая награда: ", delivery.Kind == "Collectable" ? "Collectability → scrips: " : "Base reward: ") + delivery.Reward);
            else ImGui.TextWrapped(T("Награда зависит от ценности и текущих условий. Точное количество смотри в окне сдачи.",
                "Rewards depend on collectability and current conditions. Check the delivery window for exact amounts."));
            if (delivery.ItemRewards is { Length: > 0 } rewards && plugin.Catalog is { } catalog)
            {
                if (ImGui.CollapsingHeader(T("Возможные предметы в награду", "Possible item rewards") + "###DeliveryRewards"))
                {
                    ImGui.TextWrapped(T("Варианты из таблицы наград. Шанс относится к группе, а не гарантирует каждый предмет.", "Reward table options. Probability applies to the group; each item is not guaranteed."));
                    foreach (var bonus in rewards)
                        if (catalog.Find(bonus.ItemId) is { } rewardItem)
                        {
                            ImGui.TextWrapped(T($"Группа наград: шанс {bonus.Chance}%", $"Reward group: {bonus.Chance}% chance") + (bonus.Hq ? " · HQ" : ""));
                            ImGui.PushID((int)bonus.ItemId);
                            DrawRewardCard(rewardItem, bonus.Count, snapshot);
                            ImGui.PopID();
                        }
                }
            }
            if (active) ImGui.TextWrapped(T("Предмет зарезервирован для принятого Leve, включая возможные повторы.", "Reserved for an accepted leve, including possible repeats."));
            else ImGui.TextDisabled(T("Возможное применение; текущий заказ не подтверждён", "Possible use; current request is unconfirmed"));
            if (delivery.IssuerOnly) ImGui.TextWrapped(T("Ниже NPC выдачи. Получатель сдачи здесь пока не определён — проверь журнал Leve.",
                "Quest issuer below. The hand-in recipient is unresolved here; check your leve journal."));
            if (delivery.Contacts.Length > 0)
            {
                ImGui.TextDisabled(T("Кому сдавать:", "Hand-in NPC:"));
                var contacts = delivery.Contacts.OrderByDescending(n => n.Territory == snapshot.TerritoryId).ToArray();
                DrawNpc(contacts[0]);
                if (contacts.Length > 1 && ImGui.CollapsingHeader(T("Другие места сдачи", "Other hand-in locations") + "###OtherDeliveryNpcs"))
                    foreach (var npc in contacts.Skip(1)) DrawNpc(npc);
            }
            if (delivery.Contacts.Length == 0) ImGui.TextWrapped(T("NPC сдачи: точная привязка пока недоступна.", "Hand-in NPC: exact mapping is currently unavailable."));
            ImGui.PopID();
        }
    }
}
