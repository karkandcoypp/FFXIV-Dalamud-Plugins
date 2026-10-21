namespace BagSense;

public sealed class Text(Func<bool> russian)
{
    public string T(string ru, string en) => russian() ? ru : en;
    public string Purpose(ItemFacts item) => item.QuestUses.Any(q => !q.Reward && q.Evidence.StartsWith("COST_ITEM_ATMA_"))
        ? T($"Материал для открытия улучшения Phantom Weapons. Для {item.QuestUses.First(q => !q.Reward && q.Evidence.StartsWith("COST_ITEM_ATMA_")).Name} нужно по {item.QuestUses.First(q => !q.Reward && q.Evidence.StartsWith("COST_ITEM_ATMA_")).RequiredCount} demiatma каждого вида; это общий этап, а не оплата за каждое оружие.", $"Unlock material for Phantom Weapon enhancement. {item.QuestUses.First(q => !q.Reward && q.Evidence.StartsWith("COST_ITEM_ATMA_")).Name} requires {item.QuestUses.First(q => !q.Reward && q.Evidence.StartsWith("COST_ITEM_ATMA_")).RequiredCount} of each demiatma; this is a shared unlock, not a cost per weapon.")
        : item.Role == "Unresolved" && item.ResearchUses.Any(r => r.Kind.StartsWith("Mission") || r.Kind is "WikiMission" or "CosmicMaterial")
            ? T("Предмет конкретной миссии Cosmic Exploration. Название миссии, роль материала и условия открытия показаны ниже. Это не материал улучшения боевой реликвии.", "Cosmic Exploration mission item. Its mission, material role and unlock conditions are shown below. This is not a combat relic upgrade material.")
        : item.ResearchUses.Any(r => r.Kind == "RelicExchange") ? T("Материал улучшения реликтового оружия. Расходуется при обмене для выбранного оружия; прохождение квеста не делает запас для другого оружия лишним.", "Relic weapon upgrade material, consumed for the selected weapon. Completing an unlock quest does not make stock for another weapon surplus.")
        : item.ResearchUses.Any(r => r.Kind == "Historical") ? T("Исторический предмет: прежнее применение отмечено в вики как выведенное из использования. Подробности и источник указаны ниже; решение о коллекционном хранении остаётся за тобой.", "Historical item: its former use is marked retired by the wiki. Details and provenance are below; decide whether to keep a collectible copy.")
        : item.ResearchUses.Any(r => r.Kind == "IslandUse") ? T("Ресурс, инструмент или продукция Island Sanctuary. Используется в островной системе и её отдельном хранилище; конкретный тип указан ниже.", "Island Sanctuary resource, tool or workshop product, used within the island system and its separate storage; the specific type is shown below.")
        : item.ResearchUses.Any(r => r.Kind == "CompanyPart") ? T("Деталь дирижабля или подводной лодки Free Company. Устанавливается и заменяется через мастерскую компании; нужны доступ и соответствующие права.", "Free Company airship or submersible component, fitted through the company workshop with the appropriate access and permissions.")
        : item.ResearchUses.Any(r => r.Kind == "CompanyAction") ? T("Заряженное эфирное колесо для активации усиления Free Company. Это расходный запас действий компании, а не бесполезный материал.", "Primed aetherial wheel used to activate a Free Company action: a consumable company buff supply.")
        : item.ResearchUses.Any(r => r.Kind == "Desynthesis") ? T("Предмет для разбора Desynthesis. Может дать компоненты, показанные ниже; конкретная награда не гарантируется.", "Desynthesis item that may yield the components shown below; a specific result is not guaranteed.")
        : item.ResearchUses.Any(r => r.Kind == "CurrencyUse") ? T("Запись валюты или очков игрового контента. Проверь связанную систему и обмены; такая запись каталога не обязательно является предметом обычной сумки.", "Game currency or content points record. Review its system and exchanges; a catalog entry is not necessarily an ordinary inventory item.")
        : item.ResearchUses.Any(r => r.Kind == "FragmentAppraisal") ? T("Фрагмент Bozja: оценка у Resistance Appraiser даёт случайное Lost Action. Нужный ранг, NPC и возможные результаты показаны ниже.", "Bozja fragment: a Resistance Appraiser converts it into a random Lost Action. Required rank, NPC and possible results appear below.")
        : item.ResearchUses.Any(r => r.Kind == "LogogramAppraisal") ? T("Неопознанный логограмм Eureka. Отдай Drake в Pyros или Hydatos для оценки в мнему; затем используй Logos Manipulator.", "Unidentified Eureka logogram. Ask Drake in Pyros or Hydatos to appraise it into a mneme, then use the Logos Manipulator.")
        : item.ResearchUses.Any(r => r.Kind == "LogosMneme") ? T("Мнема для создания Logos Actions через Logos Manipulator в Eureka Pyros / Hydatos.", "A mneme for creating Logos Actions at the Logos Manipulator in Eureka Pyros / Hydatos.")
        : item.ResearchUses.Any(r => r.Kind == "LostAction") ? T("Расходное действие Bozja. Используется через Lost Finds Cache и Holster внутри этого контента.", "A consumable Bozja action, used through the Lost Finds Cache and Holster within this content.")
        : item.Role == "Unresolved" && item.ResearchUses.Length > 0 ? T("Найдена дополнительная связь применения: ", "Additional use relationship: ") + item.ResearchUses[0].Context
        : item.ExchangeUses.Length > 0
        ? T("Для обмена: ", "Exchange material: ") + (string.IsNullOrWhiteSpace(item.ExchangeUses[0].Shop) ? item.ExchangeUses[0].Reward : item.ExchangeUses[0].Shop)
        : item.DeliveryUses.Length > 0 ? T("Для сдачи NPC: ", "NPC delivery material: ") + item.DeliveryUses[0].Name
        : item.Unlocked == true ? T("Предмет открытия коллекции: уже получено персонажем.", "Collection unlock item: already obtained by this character.")
        : item.QuestUses.Any(q => !q.Reward) ? T("Материал квеста: ", "Quest material: ") + item.QuestUses.First(q => !q.Reward).Name
        : item.Unlocked == false ? T("Коллекционный предмет — можно изучить из сумки.", "A collectible you can learn from your inventory.")
        : item.Equipment ? T("Экипировка. Проверь комплект, гламур и будущую прокачку.", "Equipment: consider gearsets, glamour and future leveling.")
        : item.RecipeUses.Length > 0 ? T("Материал для крафта: ", "Crafting ingredient: ") + string.Join(", ", item.RecipeUses.Take(2))
        : item.Role == "Food" ? T("Еда: временное усиление характеристик и бонус опыта. Нужна для боёв, крафта или сбора; расходуется при использовании.", "Food: temporary stat boosts and experience bonus, consumed when used. Keep what you need for combat, crafting or gathering.")
        : item.Role == "Medicine" ? T("Расходуемое зелье или лекарство. Восстанавливает ресурсы либо временно усиливает персонажа; точный эффект указан ниже.", "Consumable medicine or potion. Restores resources or grants a temporary effect; see its exact description below.")
        : item.Role == "Materia" ? T("Материя для усиления экипировки через melding. Проверь характеристики и подходящее снаряжение перед продажей.", "Materia for improving equipment through melding. Check its stats and eligible equipment before selling.")
        : item.Role == "Crystal" ? T("Кристалл для синтеза. Расходуется в рецептах соответствующей стихии.", "Crafting crystal consumed by recipes of the corresponding element.")
        : item.Role == "Bait" ? T("Наживка или приманка для рыбалки. Выбор зависит от водоёма и нужной рыбы.", "Fishing bait or lure. Its use depends on the fishing hole and target fish.")
        : item.Role == "Dye" ? T("Краска для изменения цвета подходящей экипировки. Один экземпляр расходуется на окраску.", "Dye for recoloring eligible equipment, consumed on use.")
        : item.Role == "Housing" ? T("Предмет обстановки для жилья. Размещение и ограничения указаны в описании.", "Housing furnishing. See the description for placement restrictions.")
        : item.Role == "Repair" ? T("Материал для ремонта экипировки. Требования зависят от уровня предмета и профессии.", "Equipment repair material. Requirements depend on item level and crafting job.")
        : item.Role == "Treasure" ? T("Связан с поиском сокровищ. Проверь описание, расшифровку карты и требования группы.", "Treasure hunting item. Check the description, map deciphering and party requirements.")
        : item.Role == "Usable" ? T("Используемый предмет: у него есть игровое действие. Точное назначение и ограничения смотри в описании ниже.", "Usable item with an in-game action. Its description below explains the effect and restrictions.")
        : item.Role == "Material" ? T("Материал. Проверь рецепты, сдачи и обмены ниже; категория сама по себе не доказывает, что запас лишний.", "Material. Review recipes, deliveries and exchanges below; category alone does not prove it is surplus.")
        : item.Role == "KeyItem" ? T("Ключевой предмет для квеста или игрового сценария. Он хранится отдельно от обычной сумки; план очистки его не удаляет.", "Key item for a quest or game script, stored separately from ordinary inventory and protected from cleanup plans.")
        : item.Role == "Legacy" ? T("Устаревшая или недоступная для обычного получения запись по категории игры. Это не разрешение удалить редкий или памятный экземпляр.", "Legacy or unobtainable record according to the game category. This does not authorize discarding a rare or sentimental copy.")
        : item.Role == "Outfit" ? T("Комплект внешнего вида. Проверь связанные предметы и условия получения на вики; запись комплекта не всегда равна отдельному носимому предмету.", "Appearance set. Check related items and acquisition conditions on the wiki; a set record is not always an individual wearable item.")
        : item.Role == "Gardening" ? T("Семена или материал садоводства. Используется для выращивания растений; выбор урожая и скрещивания зависит от семян и почвы.", "Gardening seed or material for growing plants. Crops and crossbreeding depend on seed and soil choices.")
        : item.Role == "Event" ? T("Предмет сезонного события. Возможность сдачи зависит от события; проверь актуальный обмен и дату на вики.", "Seasonal event item. Hand-ins depend on the event; verify the current exchange and dates on the wiki.")
        : item.Role == "Component" ? T("Компонент изготовления или сборки. Проверь рецепты и связанные проекты; отсутствие обычного рецепта не означает ненужность.", "Crafting or construction component. Check recipes and related projects; a missing ordinary recipe does not mean it is useless.")
        : item.Role == "Glamour" ? T("Расходный материал для гламура: перенос или снятие внешнего вида экипировки.", "Glamour consumable for applying or removing equipment appearance.")
        : item.Role == "Currency" ? T("Игровая валюта. Расходуется в соответствующих магазинах; доступность обмена зависит от вида валюты и открытого контента.", "Game currency spent at corresponding shops. Exchanges depend on currency type and unlocked content.")
        : item.Role == "Token" ? T("Обменный предмет или жетон по описанию игры. Полная стоимость, NPC и условия показаны ниже, если связь подтверждена; иначе проверь вики.", "Exchange token according to its game description. Verified cost, NPC and conditions appear below; otherwise check its wiki.")
        : item.Role == "GilToken" ? T("Предмет для продажи за gil. Описание игры прямо указывает обмен на деньги.", "Gil sale token: its game description explicitly identifies a monetary exchange.")
        : item.SellPrice > 0 ? T($"Можно продать обычному торговцу: {item.SellPrice} gil за NQ.", $"Sellable to an ordinary merchant: {item.SellPrice} gil per NQ item.")
        : T("Специальный предмет: точная связь пока не найдена. Его ненужность не подтверждена; проверь описание и вики перед удалением.", "Special item: its exact relationship is unresolved. Disposability is not confirmed; review its description and wiki before removing it.");
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
