# BagSense 0.1.0

First public release for Dalamud API 15.

- Russian and English interface; open `/bagsense`.
- Character-specific item notes, always-keep protection and quantity reservations.
- Personal goals with item search, quantities, allocated progress and completion.
- Explained inventory recommendations using installed game data.
- Manual cleanup plans, grouped by action, with physical-stack slot estimates.
- Protection for equipped and saved-gearset items, collectables, relics and customized equipment.
- `/bagsense demo` provides a separate sample inventory.

Add the shared repository in Dalamud settings:

```text
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
```

Then install **BagSense** through `/xlplugins`. Disable any development copy first.

BagSense reads main bags, Armoury Chest and equipped items. Inventory operations are manual. Retainers, saddlebags and glamour storage are not scanned. Initial checks cover builds, policy, persistence, installed game data and standalone UI rendering; behavior inside FFXIV still needs player verification.

Первая публичная версия: интерфейс RU/EN, цели персонажа, заметки о предметах, резервы и план очистки. После установки открой `/bagsense`. Если использовал локальную DLL, сначала отключи её в Dev Plugins. Все действия с предметами выполняются вручную.
