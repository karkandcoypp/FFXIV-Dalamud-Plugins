# BagSense 0.1.0

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

Release: [bs-v0.1.0](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/releases/tag/bs-v0.1.0).

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
- **Why I keep it / Зачем храню:** local Unicode notes, always-keep protection and a minimum stock quantity.
- Right-click a scanned inventory item and select **BagSense: why keep this? / зачем храню?** to open its card.

Russian translates the interface, advice, help and statuses. Official item, shop, recipe and category names remain English because FFXIV has no official Russian item-name sheet. Teamcraft opens only when clicked.

## Goals / Цели

Create, rename, annotate, complete, reopen and delete personal goals. Add items from an inventory card or search the installed item catalog by English name / numeric ID, including items not currently owned. Set each target quantity.

Active goals **add** their required quantities. The final reserve is the **larger** of this sum and the personal minimum stock. Goal progress allocates available units from top to bottom, so the same units cannot satisfy two goals. HQ and NQ share a base-item reservation; selecting the quality to retain is manual. Completing a goal releases its reserve while preserving its notes and targets.

Decisions are per item type, per character content ID. Goals and cleanup steps persist locally through Dalamud's plugin configuration. Logout or a character change clears the live snapshot and waits for new data. Sample goals are separate and never written to the real character configuration.

## Cleanup plan / План очистки

Choose an action and quantity in the item card, then **Add / update step**. One step per item type prevents double-counting. Steps are grouped into learning collectibles, merchant sales, desynthesis, exchange review and manual review. Copy the current plan to the clipboard.

Reservations and protection are rechecked continuously, including for steps created earlier. A quantity decrease marks a step for review; it does not prove that a sale or unlock happened. After checking the real inventory, update or remove the step manually.

Potential slots are calculated from the **smallest physical stacks first**, separately for bags and Armoury. Selling 15 from one stack of 35 frees **zero** slots. Selling all 15 units stored in stacks of 3 and 12 frees **two** slots. Review-only and stale steps promise no slots. Counts describe source slots released, not net space after desynthesis output or exchange rewards.

## Recommendation boundaries / Ограничения

- Saved gearsets are read with the installed FFXIVClientStructs layout. Equipment stays protected when gearset state is unavailable. Equipped types and types registered in a gearset (including linked glamour item IDs stored there) stay protected, even if duplicate copies exist.
- Non-disposable, collectable, relic, melded, dyed and glamoured instances protect their whole item type in this first version.
- A merchant listing is evidence of a source, **not** a statement that an item is useless or that this character can reach the NPC. Known sheet conditions are marked. Recipe and exchange examples are limited to eight; exchanges can require other currencies, items, quality and unlocks.
- Character collectible state uses Dalamud `IUnlockState`. Unsupported unlock types remain unknown; unsupported types are not presented as already learned.
- Desynthesis capability comes from the item sheet. Character job access and desynthesis output are not evaluated.
- Grand Company eligibility, individual vendor NPC map locations, market prices, complete acquisition routes and dresser ownership are outside 0.1.0. For additional item details, the card provides a Teamcraft link.
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

UI checks call the production ImGui window with isolated sample state: RU / EN, all three tabs, minimum/default/large windows and enlarged text. Text and number fields are inert stand-ins because Dalamud's native InputText extension requires the game host. These are standalone UI renders, not screenshots of FFXIV, and do not test typing or clicks. Game interaction, changing characters, context menus and native gearset reads must additionally be checked in-game.

See [VALIDATION.md](VALIDATION.md) for the evidence collected for this build.

## Release maintenance

Versions use tags `bs-v*`, for example `bs-v0.1.0` for assembly version `0.1.0.0`. The `bagsense-release.yml` workflow builds and checks the plugin, validates package metadata, publishes `BagSense.zip` and its SHA-256, then merges the released manifest into the shared `pluginmaster.json`. It preserves the other plugins and updates the existing BagSense entry on future releases. The index is updated only after the release assets are available.
