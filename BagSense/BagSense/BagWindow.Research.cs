using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
namespace BagSense;
public sealed partial class BagWindow
{
    private string selectedResearch = "";
    private void DrawResearch(ItemFacts item, InventorySnapshot snapshot)
    {
        if (item.ResearchUses.Length == 0) return;
        Heading("Точное применение и связанные этапы", "Detailed use and related progression");
        var uses = item.ResearchUses.OrderBy(r => r.Kind == "RelicExchange" ? 0 : r.Kind == "MissionHandIn" ? 1 : r.Kind == "MissionIngredient" ? 2 : 3).ToArray();
        string Key(ResearchUse u) => $"{item.Id}:{u.Kind}:{u.Context}:{u.RequiredCount}";
        var use = uses.FirstOrDefault(u => Key(u) == selectedResearch) ?? uses[0];
        ImGui.SetNextItemWidth(-1);
        if (uses.Length > 1 && ImGui.BeginCombo("###ResearchedUse", ResearchType(use.Kind) + " · " + use.Context))
        {
            foreach (var candidate in uses)
                if (ImGui.Selectable(ResearchType(candidate.Kind) + " · " + candidate.Context + "###" + Key(candidate), Key(candidate) == Key(use)))
                { use = candidate; selectedResearch = Key(candidate); }
            ImGui.EndCombo();
        }
        ImGui.PushID(Key(use));
        ImGui.TextWrapped(ResearchType(use.Kind) + ": " + use.Context);
        if (use.RequiredCount is int count)
            ImGui.TextWrapped((use.Kind == "MissionSupply" ? T("Выдают при старте миссии: ", "Supplied at mission start: ")
                : use.Kind == "MissionIngredient" ? T("На одно изготовление: ", "Per craft: ")
                : use.Kind == "QuestReference" ? T("В требованиях вики: ", "Listed wiki requirement: ") : T("По задаче: ", "Objective quantity: ")) + count);
        if (use.Kind == "QuestReference") ImGui.TextWrapped(T("Это связь из списка требований квеста. Точный этап потребления и NPC сдачи должны подтверждаться отдельно; NPC выдачи квеста не подменяет получателя предмета.",
            "This is a quest requirement association. The exact consumption objective and recipient require separate evidence; a quest giver is not substituted for the hand-in NPC."));
        if (use.RequiredCount.HasValue && use.Quality is "hq" or "any") ImGui.TextWrapped(use.Quality == "hq" ? T("Требуется HQ", "HQ required") : T("NQ подходит", "NQ accepted"));
        if (use.Kind.StartsWith("Mission") || use.Kind is "WikiMission" or "CosmicMaterial")
            ImGui.TextWrapped(T("Работай через панель Cosmic Exploration и журнал выбранной миссии. Обычного NPC сдачи здесь нет. Активность миссии и её условия качества пока не читаются автоматически; проверь их перед очисткой.",
                "Use the Cosmic Exploration panel and the selected mission's objectives. There is no ordinary hand-in NPC here. Active mission and quality conditions are not yet read automatically; check them before cleanup."));
        if (use.Kind is "RelicExchange" or "WikiExchange")
            ImGui.TextWrapped(T("Расход на один указанный вариант обмена. Для другого оружия или повторного обмена нужен новый комплект; завершённый квест открытия не означает, что материал лишний.",
                "Cost for this exchange option. Another weapon or exchange needs another set; completing its unlock quest does not make the material surplus."));
        if (use.Kind == "SquadronClass") ImGui.TextWrapped(T("Книга меняет класс бойца Adventurer Squadron. Используй управление отрядом в казармах своей Grand Company; конкретные доступные классы перечислены в описании игры. Книга расходуется при смене класса.", "Changes an Adventurer Squadron member's class. Use squadron management in your Grand Company barracks; the game description lists the applicable classes. The book is consumed when changing class."));
        if (use.Kind == "PvpSeason") ImGui.TextWrapped(T("Подтверждение сезонных PvP-достижений. Это привязанная к указанному сезону запись; действующий обмен, цена и NPC для этой записи не подтверждены. Нельзя считать её лишней или обещать награду только по названию — проверь источник.", "A record of seasonal PvP accomplishments, tied to the named season. An active exchange, price and NPC are unconfirmed for this entry. Its name alone does not establish disposability or a guaranteed reward; check the source."));
        if (use.Kind == "LotteryTicket") ImGui.TextWrapped(T("Билет Cosmic Fortune: обменивается у Orbitingway на случайный приз. Конкретная награда заранее не гарантирована; проверь текущий список у NPC.", "A Cosmic Fortune ticket: exchange it with Orbitingway for a random prize. No particular reward is guaranteed; check the current pool with the NPC."));
        if (use.Kind == "LogogramAppraisal") ImGui.TextWrapped(T("Отдай неопознанный логограмм Drake в Eureka Pyros или Hydatos. После оценки получишь случайную мнему из возможного списка; затем комбинируй мнемы в Logos Manipulator. Ни один конкретный результат не гарантирован.", "Have Drake in Eureka Pyros or Hydatos appraise this unidentified logogram. It yields a random mneme from its pool; combine mnemes in the Logos Manipulator. No particular result is guaranteed."));
        if (use.Kind == "FragmentAppraisal") ImGui.TextWrapped(T("Отдай фрагмент Resistance Appraiser: результат случайный, это один из перечисленных Lost Actions. Текущий Resistance rank не проверяется автоматически; сравни его с условием ниже. Полученное действие хранится отдельно от обычной сумки и не является постоянным изучением маунта.", "Have a Resistance Appraiser appraise this fragment for a random Lost Action from its pool. Current Resistance rank is not read automatically; compare it with the requirement below. The resulting action uses special storage and is not a permanent mount-style unlock."));
        if (use.Kind == "LogosMneme") ImGui.TextWrapped(T("Мнема для Logos Manipulator в Eureka Pyros / Hydatos. Комбинируй мнемы в Logos Actions; они хранятся в специальной системе. Обратись к Drake рядом с манипулятором. Это не обычная сдача квеста и не постоянное коллекционное открытие.", "A mneme for the Logos Manipulator in Eureka Pyros / Hydatos. Combine mnemes into Logos Actions in their special storage system; Drake is beside the manipulator. This is not an ordinary quest hand-in or permanent collectible unlock."));
        if (use.Kind == "LostAction") ImGui.TextWrapped(T("Расходное Lost Action для Bozja: получай через оценку соответствующих Forgotten Fragments, затем переноси из Lost Finds Cache в Lost Finds Holster. Использование ограничено этим контентом. Запись действия не означает, что это обычный предмет сумки или постоянное открытие.", "A consumable Bozja Lost Action: obtain it by appraising the corresponding Forgotten Fragments, then transfer it from the Lost Finds Cache to the Lost Finds Holster. It is used within this content. An action record is not necessarily an ordinary bag item or permanent unlock."));
        if (use.Kind == "ProgressPoints") ImGui.TextWrapped(T("Очки специальной системы, а не обычный предмет сумки. Точные правила начисления, потери и расходования зависят от системы; открой связанное руководство. Состояние этой шкалы у персонажа пока не читается.", "Points in a special progression system rather than an ordinary bag item. Earning, loss and spending rules depend on that system; open its guide. This character's point total is not currently read."));
        if (use.Kind == "Placeholder") ImGui.TextWrapped(T("Таблица игры помечает запись как placeholder. Подтверждённого игрового применения не найдено; не следует приписывать ей квест или NPC.", "The game classifies this entry as a placeholder. No playable use is confirmed; a quest or NPC must not be invented for it."));
        if (use.Kind == "IslandUse") ImGui.TextWrapped(T("Работа с этим предметом идёт через Island Sanctuary: островные рецепты, мастерские, сад, пастбище или экспорт в зависимости от типа. Это не обычная сдача квеста в открытом мире.", "This item belongs to Island Sanctuary crafting, workshops, crops, pasture or exports depending on its type, rather than an ordinary open-world quest hand-in."));
        if (use.Kind == "CompanyPart") ImGui.TextWrapped(T("Используй панель дирижабля или подводной лодки в мастерской Free Company. Единой публичной точки сдачи NPC нет: мастерская привязана к дому компании.", "Use the airship or submersible panel in the Free Company workshop. There is no universal public NPC hand-in point: the workshop belongs to the company estate."));
        if (use.Kind == "CompanyAction") ImGui.TextWrapped(T("Используется для действий Free Company через эфирные колёса. Проверь права компании и действующие усиления перед расходованием.", "Used for Free Company actions through aetherial wheels. Check company permissions and active buffs before using it."));
        if (use.Kind == "Desynthesis") ImGui.TextWrapped(T("Разбор доступен через меню предмета. Указанные результаты — возможные; данные наблюдений не гарантируют выпадение нужного компонента.", "Desynthesize through the item menu. Listed results are possibilities; observed data does not guarantee the desired component."));
        if (use.Kind == "Historical") ImGui.TextWrapped(T("Прежняя механика выведена из использования по данным вики. Решение о памятном или коллекционном хранении остаётся за тобой.",
            "The former mechanic is marked retired by the wiki. Decide whether to keep a sentimental or collectible copy."));
        if (use.Kind == "ContentUse") ImGui.TextWrapped(T("Подтверждён связанный игровой механизм. Это контекст применения; точное количество или конкретный NPC не выводятся из одной ссылки.",
            "A related game system is identified. This is use context; an exact quantity or NPC is not inferred from a guide link alone."));
        foreach (var cost in use.Costs ?? [])
            ImGui.TextWrapped($"{cost.Count} × {cost.Name}" + (cost.Hq ? " · HQ" : "") + (cost.Collectability > 0 ? T($" · ценность ≥ {cost.Collectability}", $" · collectability ≥ {cost.Collectability}") : ""));
        foreach (var requirement in use.Requirements ?? []) ImGui.TextWrapped(T("Условие источника: ", "Source condition: ") + (requirement.StartsWith("Resistance rank ≥ ") ? T(requirement.Replace("Resistance rank", "Ранг Resistance").Replace("≥", ">="), requirement) : requirement switch {
            "Free Company workshop access and voyage permissions" => T("доступ к мастерской Free Company и права управления экспедициями", requirement),
            "Aetherial Wheel Stand and Free Company permissions" => T("эфирная подставка и соответствующие права Free Company", requirement),
            "Game description explicitly marks obsolete" => T("описание установленной игры прямо указывает, что предмет больше не действует", requirement),
            "Retired / obsolete; see the referenced revision" => T("прежняя механика выведена из использования; указана ревизия источника", requirement),
            "Unresolved additional cost; verify complete price with NPC" => T("часть цены не сопоставлена; проверь полный комплект у NPC", requirement), _ => requirement }));
        var contacts = use.Contacts ?? [];
        if (contacts.Length == 0 && plugin.Catalog is { } catalog)
            contacts = catalog.World.Positions.Values.SelectMany(p => p).Where(p => (use.Npcs ?? []).Any(n => Regex.Replace(n, @"\s*\([^)]*\)", "").Equals(p.Npc, StringComparison.OrdinalIgnoreCase))
                && (use.Zone.Length == 0 || use.Zone.Contains(p.Zone, StringComparison.OrdinalIgnoreCase) || p.Zone.Contains(use.Zone, StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(n => n.Key).ToArray();
        foreach (string npc in use.Npcs ?? []) ImGui.TextWrapped(T("NPC обмена / оценки: ", "Exchange / appraisal NPC: ") + npc);
        if (use.Zone.Length > 0) Hint(use.Zone);
        if (contacts.FirstOrDefault() is { } contact) DrawNpc(contact);
        else if (use.MapX is float x && use.MapY is float y && plugin.Catalog?.ResearchMap(use.Zone) is not null)
        {
            Hint(T($"Координаты источника: X {x:0.0}, Y {y:0.0}", $"Source coordinates: X {x:0.0}, Y {y:0.0}"));
            ImGui.BeginDisabled(staticPreview || plugin.Demo);
            if (ImGui.Button(T("Открыть карту по координатам вики", "Open map at wiki coordinates") + "###WikiPoint")) plugin.OpenResearchMap(use.Zone, x, y);
            ImGui.EndDisabled();
        }
        else if (use.Npcs is { Length: > 0 }) ImGui.TextWrapped(T("Точная привязка карты ещё не подтверждена. Имя и локация взяты из источника; открой вики NPC.", "Exact map binding is unconfirmed. The name and area come from the source; open the NPC wiki."));
        foreach (string npc in use.Npcs ?? []) Wiki(npc, T("Вики NPC", "NPC wiki"), "ResearchNpc" + npc);
        foreach (uint q in use.Quests ?? [])
        {
            DrawQuestAccess(snapshot, q);
            if (plugin.Catalog?.QuestStarts.GetValueOrDefault(q)?.FirstOrDefault() is { } issuer)
            {
                Hint(T("Где взять связанный квест (это NPC выдачи):", "Related quest giver (not a confirmed item recipient):"));
                DrawNpc(issuer);
            }
        }
        var results = (use.Results ?? []).Select(id => plugin.Catalog?.Find(id)).Where(r => r != null).Select(r => r!).ToArray();
        if (results.Length > 0 && ImGui.CollapsingHeader(T(use.Kind == "Container" ? "Что может быть внутри" : "Результаты и награды", use.Kind == "Container" ? "Possible contents" : "Results and rewards") + "###ResearchResults"))
        {
            ImGui.TextWrapped(T("Возможные варианты, а не обещание получить всё сразу.", "Possible options, not a promise to receive all of them."));
            foreach (var result in results.Take(20)) { ImGui.PushID((int)result.Id); DrawRewardCard(result, 1, snapshot); ImGui.PopID(); }
            if (results.Length > 20) ImGui.TextWrapped(T($"Всего {results.Length} вариантов. Полный перечень — в источнике.", $"{results.Length} options in total. See the source for the complete list."));
        }
        if (use.WikiTitle.Length > 0) Wiki(use.WikiTitle.Split('#')[0], T("Открыть источник применения", "Open use source"), "UseSource");
        Hint(use.Evidence.StartsWith("Game ") ? T("Источник: категория или описание игры", "Source: game category or description") : use.Revision > 0 ? T($"Факт из вики · ревизия {use.Revision}", $"Wiki fact · revision {use.Revision}") : use.Kind.StartsWith("Mission") ? T("Источник: таблицы миссий установленной игры", "Source: installed mission sheets") : T("Источник: структурированные записи вики", "Source: structured wiki records"));
        ImGui.PopID();
    }
    private string ResearchType(string kind) => kind switch
    {
        "MissionHandIn" => T("Задача космической миссии", "Cosmic mission objective"), "MissionCraft" => T("Изделие космической миссии", "Cosmic mission craft"),
        "MissionIngredient" => T("Материал космического рецепта", "Cosmic recipe ingredient"), "MissionSupply" => T("Выдаваемый материал миссии", "Mission supply"),
        "CosmicMaterial" => T("Материал Cosmic Exploration", "Cosmic Exploration material"), "MissionGather" => T("Добыча в космической миссии", "Gathering mission item"),
        "IslandUse" => T("Островная система", "Island system"), "CompanyPart" => T("Деталь экспедиционного судна", "Voyage vessel component"),
        "CompanyAction" => T("Усиление Free Company", "Free Company action"), "CurrencyUse" => T("Валюта или очки", "Currency or points"), "Desynthesis" => T("Разбор на компоненты", "Desynthesis components"),
        "WikiMission" => T("Космическая миссия", "Cosmic mission"), "QuestReference" => T("Квестовое требование", "Quest requirement"),
        "RelicExchange" => T("Улучшение реликтового оружия", "Relic weapon upgrade"), "WikiExchange" => T("Обмен у NPC", "NPC exchange"),
        "SquadronClass" => T("Смена класса бойца отряда", "Squadron class change"), "PvpSeason" => T("Сезонная PvP-запись", "Seasonal PvP record"), "LotteryTicket" => T("Билет розыгрыша", "Prize draw ticket"),
        "LogogramAppraisal" => T("Оценка логограмма Eureka", "Eureka logogram appraisal"), "FragmentAppraisal" => T("Оценка фрагмента Bozja", "Bozja fragment appraisal"), "LogosMneme" => T("Компонент Logos Action", "Logos Action ingredient"), "LostAction" => T("Расходное действие Bozja", "Consumable Bozja action"), "ProgressPoints" => T("Очки прогресса", "Progression points"), "Placeholder" => T("Служебная запись", "Placeholder entry"),
        "Historical" => T("Историческое применение", "Historical use"), "Container" => T("Контейнер наград", "Reward container"), _ => T("Игровой механизм", "Game system")
    };
}
