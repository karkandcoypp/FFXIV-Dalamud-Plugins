# BagSense 0.2.0 — item purpose and NPC destinations

- Opaque dark interface, compact item rows and a wider, clearer item card.
- Item purpose is visible first; personal notes and reserve controls are in a separate expandable section.
- Concrete NPCs, zones, coordinates and **Open map** buttons for supported exchanges and ordinary gil sales.
- Complete simultaneous exchange prices, reward selection, quantity comparisons and known access quests.
- Requested quest materials are distinguished from quest rewards, with completion status and contact locations.
- Cleanup plans group by NPC and zone, keep the selected exchange/route and include map buttons and destination details in exports.
- Existing goals, notes, reservations and plans remain compatible.

Examples checked against installed game data: Aetherpool Grip → E-Una-Kotor; Aloalo Potsherd → Trisassant; Aetherspun Gold → expedition armorer in South Horn. South Horn coordinates come from the installed zone placement file.

Update **BagSense** through `/xlplugins`, then open `/bagsense`. The repository URL is unchanged:

```text
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
```

Обновление 0.2.0: читаемый интерфейс, назначение предмета на первом месте, конкретные NPC и кнопки карты, полная цена обмена и связи с квестами. Выбранное место сохраняется в плане. Обновись в `/xlplugins` и открой `/bagsense`.

Inventory actions remain manual. Unknown NPCs, currencies and conditions are labelled rather than guessed. Quest coverage is partial for special scripted chains. Builds, policies, persistence, installed data and standalone rendering are checked; map clicks still need in-game verification.
