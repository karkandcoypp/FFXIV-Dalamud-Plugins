namespace BagSense;

public sealed class Text(Func<bool> russian)
{
    public string T(string ru, string en) => russian() ? ru : en;
    public string Purpose(ItemFacts item) => item.ExchangeUses.Length > 0
        ? T("Для обмена: ", "Exchange material: ") + (string.IsNullOrWhiteSpace(item.ExchangeUses[0].Shop) ? item.ExchangeUses[0].Reward : item.ExchangeUses[0].Shop)
        : item.QuestUses.Any(q => !q.Reward) ? T("Материал квеста: ", "Quest material: ") + item.QuestUses.First(q => !q.Reward).Name
        : item.Unlocked == false ? T("Коллекционный предмет — можно изучить из сумки.", "A collectible you can learn from your inventory.")
        : item.Equipment ? T("Экипировка. Проверь комплект, гламур и будущую прокачку.", "Equipment: consider gearsets, glamour and future leveling.")
        : item.RecipeUses.Length > 0 ? T("Материал для крафта: ", "Crafting ingredient: ") + string.Join(", ", item.RecipeUses.Take(2))
        : item.SellPrice > 0 ? T($"Можно продать обычному торговцу: {item.SellPrice} gil за NQ.", $"Sellable to an ordinary merchant: {item.SellPrice} gil per NQ item.")
        : T("Полное применение не определено. Посмотри описание предмета.", "Its full use is not known. Check the item description.");
    public string Action(PlanAction action) => action switch
    {
        PlanAction.Use => T("Изучить предмет", "Learn collectible"),
        PlanAction.Vendor => T("Продать торговцу", "Sell to a merchant"),
        PlanAction.Desynthesize => T("Разобрать предмет", "Desynthesize"),
        PlanAction.ExchangeReview => T("Обменять у NPC", "Exchange with an NPC"),
        _ => T("Разобрать вручную", "Review manually"),
    };
    public string Kind(AdviceKind kind) => kind switch
    {
        AdviceKind.Protected => T("Сохранить", "Protected"),
        AdviceKind.Reserved => T("Весь запас нужен", "Fully reserved"),
        AdviceKind.Learn => T("Можно изучить", "Ready to learn"),
        AdviceKind.Vendor => T("Продажа за gil", "Sell for gil"),
        AdviceKind.Gear => T("Проверить экипировку", "Review equipment"),
        AdviceKind.Exchange => T("Обмен у NPC", "NPC exchange"),
        AdviceKind.Deferred => T("Отложено", "Deferred"),
        _ => T("Нужно решение", "Your decision"),
    };
    public string Reason(Reason reason) => reason switch
    {
        BagSense.Reason.UnknownItem => T("Данные предмета недоступны. Рекомендация не сформирована.", "Item data is unavailable. No recommendation was made."),
        BagSense.Reason.AlwaysKeep => T("Ты отметил этот предмет: всегда сохранять.", "You marked this item: always keep."),
        BagSense.Reason.Equipped => T("Этот тип предмета сейчас надет на персонажа.", "An item of this type is currently equipped."),
        BagSense.Reason.Gearset => T("Этот тип предмета записан в сохранённом комплекте.", "An item of this type is registered in a saved gearset."),
        BagSense.Reason.GearsetsUnknown => T("Сохранённые комплекты не прочитаны. Экипировка защищена до проверки.", "Saved gearsets could not be read. Equipment stays protected until checked."),
        BagSense.Reason.Indisposable => T("В данных игры предмет помечен как не подлежащий удалению.", "Game data marks this item as non-disposable."),
        BagSense.Reason.SpecialInstance => T("Есть коллекционный экземпляр, реликт, материя, окраска или гламур. Проверь его отдельно.", "A collectable, relic, melded, dyed or glamoured instance needs individual review."),
        BagSense.Reason.FullReserve => T("Всё доступное количество зарезервировано твоими целями или личным запасом.", "All available units are reserved by your goals or personal stock."),
        BagSense.Reason.GoalReserve => T("Часть запаса нужна для цели. Остаток можно рассмотреть отдельно.", "Part of your stock is reserved. Review the surplus separately."),
        BagSense.Reason.Learn => T("Проверка Dalamud: коллекционный предмет ещё не изучен. После использования проверь результат.", "Dalamud reports this collectible as not yet learned. Check the result after using it."),
        BagSense.Reason.AlreadyLearned => T("Предмет уже изучен. Повторное использование может иметь ограничения; проверь варианты вручную.", "This collectible is already learned. Review the remaining copies manually."),
        BagSense.Reason.VendorSource => T("Найдена запись обычного магазина без выявленных условий. Перед продажей проверь доступ к магазину и свои планы.", "A regular shop listing has no detected conditions. Check shop access and your plans before selling."),
        BagSense.Reason.GearReview => T("Не надет и не записан в комплекте. Проверь внешний вид, будущую прокачку и доступность разбора.", "Not equipped or registered in a gearset. Check appearance, future leveling and desynthesis access."),
        BagSense.Reason.ExchangeUses => T("В данных игры предмет используется для обмена. Посмотри награды перед решением.", "Game data lists exchanges using this item. Review the rewards before deciding."),
        BagSense.Reason.RecipeUses => T("Предмет участвует в рецептах. Отсутствие цели не означает, что он не нужен.", "This item is used in recipes. Having no goal does not make it useless."),
        BagSense.Reason.Deferred => T("Ты отложил решение. Предмет остаётся в списке всех вещей.", "You deferred this decision. The item remains in All items."),
        BagSense.Reason.QuestItem => T("Предмет запрашивается в квесте. Сначала проверь его состояние и текущий этап.", "A quest requests this item. Check the quest status and current step first."),
        BagSense.Reason.GilToken => T("В описании игры указано: предмет обменивается на gil у обычного торговца.", "The game description says this item can be exchanged for gil at a regular merchant."),
        _ => T("Применения могут быть неполными. Укажи причину хранения или проверь предмет вручную.", "Known uses may be incomplete. Add a reason to keep it or review it manually."),
    };
}
