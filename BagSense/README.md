## Researched uses and relics (0.7.0)

New RU/EN use cards retain complete relic exchange prices, possible rewards, NPCs/maps and source wiki links/revisions. Cosmic mission materials use native mission-to-item relationships. Bozja appraisal fragments and Eureka logograms show random result pools instead of promised rewards; their special storage systems are explained.

Of the previous 2,577 unresolved entries, 2,568 now have a verified relationship or system context. Nine remain unresolved. Context alone does not prove a precise recipient, current access or safe disposal. Exact quantities cover 2,025 quest associations; unknown stages, ranks and active mission state remain explicit. See [coverage-audit.json](coverage-audit.json) and [VALIDATION.md](VALIDATION.md).

## Item knowledge and access (0.6.0)

Every current catalog record is audited; see [coverage-audit.json](coverage-audit.json) for exact scope and remaining unresolved IDs. "Linked" and "explained category" are distinct evidence levels, neither a promise that every server script is decoded. Unknown use is not a discard recommendation. Reacquisition from a gil merchant no longer defaults to a sale plan.

Item cards show purpose, original description, wiki lookup, uses and acquisition. The graph includes complete indexed recipe inputs/results, gathering/fishing, ordinary shops, reverse special-shop exchanges, quest rewards and Teamcraft drop/duty/FATE/container facts. Expandable lists retain all rows. Quest cards explain level/current profession and AND/OR prerequisite status, with wiki links and navigation to prerequisite chains. Additional server conditions are named and explicitly unverified.

COST_ITEM quest semantics and shared-map NPC placement fix Azurite Demiatma and related progression: 3 of each for Arcane Artistry, Gerolt hand-in, the Phantom Weapon shared unlock. This known one-time stock stays protected before acceptance until completion is confirmed.

Source facts: [ffxiv-datamining](https://github.com/xivapi/ffxiv-datamining), [FFXIV Teamcraft data](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/tree/staging/libs/data/src/lib/json), [Azurite Demiatma wiki](https://ffxiv.consolegameswiki.com/wiki/Azurite_Demiatma), [Arcane Artistry wiki](https://ffxiv.consolegameswiki.com/wiki/Arcane_Artistry). Both upstream revisions are pinned in the database manifest on each daily collection. No player information is uploaded.

## Deliveries and collection decisions (0.5.0)

BagSense indexes installed tradecraft/fishing Leve requirements and actual delivery NPCs. Accepted leves reserve the first hand-in plus all possible repeats. The item view shows exact per-delivery quantity, NQ/HQ guidance, matching stock, base EXP/gil and optional item reward cards. Custom Delivery and collectable shop entries provide quality requirements, rewards and NPC maps; a possible weekly item is never labelled as a confirmed current request. Actual stack collectability is considered.

Collection ownership is checked for supported unlock items, including rewards outside the inventory. The decision banner prioritizes protections and reserves, then use/owned-duplicate/review. Unknown collection status cannot authorize duplicate removal. No automatic sale, hand-in or discard is performed. Dynamic GC daily requests and live weekly selection are outside this release.

# BagSense 0.4.0

**Русский:** помощник, который сохраняет причины хранения предметов и составляет объяснимый план освобождения места. Интерфейс на русском и английском, переключается сразу. Все продажи, использование, разбор и обмены игрок выполняет вручную.

**English:** remember why you keep items and build an explained cleanup plan. Russian and English UI, switched immediately. All sales, item use, desynthesis and exchanges are performed manually by the player.

Requires Dalamud API 15 and .NET 10. Command: `/bagsense`. `/bagsense demo` opens a separate sample inventory; `/bagsense live` returns to the current character.

## Install through Dalamud / Установка через Dalamud

1. In `/xlsettings` → **Experimental → Custom Plugin Repositories**, add:

   ```text
   https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
   ```

2. Save, open `/xlplugins`, search for **BagSense**, and install it.
3. Open `/bagsense`. Use **RU / EN** at the top of the window to change language.

Если общий репозиторий уже подключён для других наших плагинов, второй раз добавлять адрес не нужно. Обнови список в `/xlplugins` и найди **BagSense**. Если ранее использовал локальную DLL, сначала отключи её в Dev Plugins. Дальнейшие версии приходят через обычное обновление Dalamud.

Release: [bs-v0.4.0](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/releases/tag/bs-v0.4.0).

## Quest protection and online catalog / Квесты и обновляемая база

Вкладка **База предметов** содержит весь каталог именованных предметов (54 411, включая 3 531 ключевой предмет), поиск по названию/ID и просмотр предметов, которых нет в сумках. Источник — публичный [ffxiv-datamining](https://github.com/xivapi/ffxiv-datamining). Наш [ежедневный сборщик](../.github/workflows/bagsense-data.yml) закрепляет ревизию исходников, публикует сжатую базу и SHA-256 в [data](data). Плагин проверяет обновления при загрузке и раз в сутки; доступна ручная проверка. Без сети используется проверенная локальная копия, затем данные установленной игры. Данные персонажа не отправляются.

Для предмета показываются текст задачи, её номер, точное количество (если подтверждено), качество и NPC именно этого этапа с картой. Состояние активных квестов читается локально. Для завершающего этапа определяется состояние сдачи; произвольные промежуточные флаги скрипта не выдаются за подтверждённую готовность.

Резервы активных квестов автоматически исключаются из всех количеств плана очистки и складываются с отдельными личными целями. До завершения квеста запас сохраняется: исчезновение предметов не считается доказательством сдачи. Для неизвестного количества, неизвестного качества или недоступного журнала сохраняется весь возможный запас. HQ-требования также защищают все экземпляры, поскольку план оперирует общим количеством типа предмета. Неактивные квесты не создают автоматический резерв; для будущего квеста можно создать личную цель. Защита ограничивает план BagSense и **не перехватывает ручные продажи в интерфейсе игры**.

Current coverage is 5,193 journal quests, 4,177 item/quest links and 687 known quantities. Full item coverage is not complete quest-script coverage. Quantities are extracted conservatively from literal journal phrases using the item's singular/plural forms; evidence is retained. `ToDoQty` is never treated as a hand-in quantity. Unknown NPCs/amounts remain explicit. Names and original objectives follow the English game data; the plugin interface is RU/EN.

## Item purpose and NPC destinations / Назначение и NPC

The item card starts with **Purpose / Назначение**, followed by the selected reward, its complete simultaneous costs, the concrete NPC, zone, coordinates and **Open map / Открыть карту**. Notes, protection and reserves are in **Keep for myself / a goal / Оставить для себя / для цели**. The window uses an opaque dark theme, compact item rows and a wider details pane.

- Select the reward before adding an exchange to the plan. All costs are displayed, including the gear being upgraded, other materials and collectability requirements.
- Available quantities describe scanned bags/Armoury only. Currency tabs and unresolved currencies remain unknown. The count does not confirm access or quality conditions.
- **Where to go / Куда идти** identifies the NPC and location. Choose among alternative NPCs when available. The map button places that NPC's marker; it does not teleport or move the character.
- Ordinary gil sales use regular merchants, separate from the NPCs who sell an item or accept a special exchange.
- Quest inputs use named `RITEM0`, `RITEM1` script parameters. Requested items and quest rewards are separate. Completion, repeatability and the quest completion contact or giver are shown. Check the current journal step manually; special scripted chains can have additional unindexed uses.
- NPC links are traced through ENpcBase, PreHandler, TopicSelect, InclusionShop categories/series, FateShop and explicitly named CustomTalk SHOP instructions. Conditions stay attached to individual routes.
- Coordinates come from matching Level/Map/territory records. South Horn's missing shop placements are read from its installed `planevent.lgb`. Unknown destinations never receive invented markers.

The plan groups items by NPC and zone. Selected reward, complete-price identity and route survive reloads. Missing saved routes require reselection. Existing 0.1 goals, notes, reservations and plans remain compatible.

## Local developer installation / Локальная установка для разработки

1. Run `./build.ps1` (or use the supplied `artifacts/BagSense.zip`).
2. Extract **both** `BagSense.dll` and `BagSense.json` together to a permanent folder.
3. Open Dalamud settings with `/xlsettings`, enable developer options if needed, and add the absolute path to **BagSense.dll** under **Dev Plugin Locations**. Save settings.
4. In `/xlplugins`, open **Dev Tools / Installed Dev Plugins**, enable BagSense, then type `/bagsense`.

Для локального теста распакуй архив, добавь абсолютный путь к `BagSense.dll` в **Dev Plugin Locations** настроек Dalamud и включи плагин в списке плагинов разработчика. Одновременно должна быть включена только одна копия плагина.

## Inventory / Инвентарь

The four main inventory pages, Armoury Chest (except soul crystals), and currently equipped items are read every three seconds on the framework thread. Retainers, saddlebags, dresser, armoire, housing and other characters' inventories are not scanned. Counts and goal progress refer only to these containers.

- **Review / Разобрать** lists decisions still worth making. Filters for containers, names and item IDs.
- **My stock / Мои запасы** shows protected items, reservations and items with saved decisions.
- **All items / Все предметы** includes protected and deferred items.
- Select an item to see its explanation, known merchant listings, example recipes and supported direct item exchanges.
- **Review my bags / Разобрать сумку** shows one item at a time. Adding a plan step advances to another item; **Next / Следующий** defers the item for one hour.
- **Keep for myself / a goal / Оставить для себя / для цели:** local Unicode notes, always-keep protection and a minimum stock quantity.
- Right-click a scanned inventory item and select **BagSense: why keep this? / зачем храню?** to open its card.

Russian translates the interface, advice, help and statuses. Official item, shop, recipe and category names remain English because FFXIV has no official Russian item-name sheet. Teamcraft opens only when clicked.

## Goals / Цели

Create, rename, annotate, complete, reopen and delete personal goals. Add items from an inventory card or search the installed item catalog by English name / numeric ID, including items not currently owned. Set each target quantity.

Active goals **add** their required quantities. The final reserve is the **larger** of this sum and the personal minimum stock. Goal progress allocates available units from top to bottom, so the same units cannot satisfy two goals. HQ and NQ share a base-item reservation; selecting the quality to retain is manual. Completing a goal releases its reserve while preserving its notes and targets.

Decisions are per item type, per character content ID. Goals and cleanup steps persist locally through Dalamud's plugin configuration. Logout or a character change clears the live snapshot and waits for new data. Sample goals are separate and never written to the real character configuration.

## Cleanup plan / План очистки

Choose an action and quantity, then **Add to my plan**. One step per item type prevents double-counting. Steps group by destination NPC and zone, or by action when no destination applies. The plan includes map buttons; clipboard export includes destinations and exchange costs.

Reservations and protection are rechecked continuously, including for steps created earlier. A quantity decrease marks a step for review; it does not prove that a sale or unlock happened. After checking the real inventory, update or remove the step manually.

Potential slots are calculated from the **smallest physical stacks first**, separately for bags and Armoury. Selling 15 from one stack of 35 frees **zero** slots. Selling all 15 units stored in stacks of 3 and 12 frees **two** slots. Review-only and stale steps promise no slots. Counts describe source slots released, not net space after desynthesis output or exchange rewards.

## Recommendation boundaries / Ограничения

- Saved gearsets are read with the installed FFXIVClientStructs layout. Equipment stays protected when gearset state is unavailable. Equipped types and types registered in a gearset (including linked glamour item IDs stored there) stay protected, even if duplicate copies exist.
- Non-disposable, collectable, relic, melded, dyed and glamoured instances protect their whole item type in this first version.
- A merchant listing is evidence of a source, **not** a statement that an item is useless or that this character can reach the NPC. Known sheet conditions are marked. Recipe examples are limited to eight; supported direct exchanges retain all rewards and simultaneous costs. Other currency encodings remain outside the exchange index.
- Character collectible state uses Dalamud `IUnlockState`. Unsupported unlock types remain unknown; unsupported types are not presented as already learned.
- Desynthesis capability comes from the item sheet. Character job access and desynthesis output are not evaluated.
- Grand Company eligibility, market prices, complete acquisition routes, every special quest script and dresser ownership are outside 0.3.0. Some NPCs and access conditions remain unresolved and are labelled. The card provides a Teamcraft link for additional details.
- The plugin has no automatic inventory operations and sends no character data or telemetry.

## Build and checks

```powershell
./build.ps1
# Cached packages, with NuGet vulnerability endpoint unavailable:
./build.ps1 -Offline
dotnet run --project tests/PolicyChecks/PolicyChecks.csproj -c Release
dotnet run --project tools/ApiProbe/ApiProbe.csproj -c Release -- --data 'C:/path/to/FFXIV/game/sqpack'
dotnet run --project tests/UiChecks/UiChecks.csproj -c Release -- artifacts/ui
python tests/UiChecks/render.py artifacts/ui
```

`build.ps1` finds the workspace's local .NET 10 if available, treats compiler warnings as errors, runs policy checks, verifies manifest/assembly versions, and packages only the plugin DLL and manifest. `-Offline` disables the online NuGet vulnerability audit for that invocation; dependencies still need to be cached. The UI renderer requires numpy and Pillow.

UI checks call the production ImGui window with isolated sample state: RU / EN, inventory/goals/plan tabs at minimum/default/large sizes and enlarged text, plus the database tab. Text and number fields are inert stand-ins because Dalamud's native InputText extension requires the game host. These are standalone UI renders, not screenshots of FFXIV, and do not test typing or clicks. Game interaction, changing characters, context menus and native gearset reads must additionally be checked in-game.

See [VALIDATION.md](VALIDATION.md) for the evidence collected for this build.

## Release maintenance

Versions use tags `bs-v*`, for example `bs-v0.1.0` for assembly version `0.1.0.0`. The `bagsense-release.yml` workflow builds and checks the plugin, validates package metadata, publishes `BagSense.zip` and its SHA-256, then merges the released manifest into the shared `pluginmaster.json`. It preserves the other plugins and updates the existing BagSense entry on future releases. The index is updated only after the release assets are available.

## 0.4.0: all journal quests and reward cards

The new **Quests / Квесты** tab enumerates all 5,193 named journal quests in the current source, rather than only quests with RITEM parameters. It lists objective text, item requirements, accepting-job alternatives and supported objective NPC maps. The item database now also includes 3,531 key items in their original EventItem ID space. Ordinary bags and key items are not conflated.

Requirements combine semantic script references, literal hand-in objectives, explicit journal/dialogue requests and structured QuestClassJobSupply records. Initial coverage: 4,177 quest-item links and 687 known quantities, compared with 118 in 0.3.0. Job variants use the class/job on which the quest was accepted. Unknown/HQ needs remain conservative. Full enumeration does not solve every server script, and separate Leve/delivery systems are not represented as normal Quest rows.

Exchange cards explain **what you get**: icon, localized type and usage instructions, description, known collection state and complete required/owned/missing costs. Mount cards link the whistle/key to its Mount record and load the actual journal artwork from installed game data. No external image site is required. Filters cover mounts, minions, music and gear. Existing selected exchange routes and plans remain compatible.
