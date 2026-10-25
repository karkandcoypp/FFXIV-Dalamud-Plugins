# FFXIV Dalamud Plugins

Общий репозиторий плагинов **Carcosa Lyuman** для FFXIV / Dalamud API 15.

| Плагин | Что делает | Команда | Исходники |
| --- | --- | --- | --- |
| Ultrawide Cutscenes Community | Убирает полосы 16:9 из внутриигровых кат-сцен на широких мониторах | `/pcutscenes` | [UltrawideCutscenes](UltrawideCutscenes/README.md) |
| Eorzea Event Tracker | Показывает текущие события, инструкции, награды и прогресс персонажа | `/events` | [EorzeaEventTracker](EorzeaEventTracker/README.md) |
| Eorzea Compass (предварительная версия) | Журнал прогресса: 220 цепочек и атлас коллекций с магазинами обществ/зон и автоматической отметкой собранного | `/compass` | [EorzeaCompass](EorzeaCompass/README.md) |
| BagSense | Обновляемая полная база предметов, квестовые резервы, этапы сдачи, NPC и карта, личные цели и план очистки, RU / EN | `/bagsense` | [BagSense](BagSense/README.md) |

Eorzea Compass доступен через общий репозиторий для проверки в игре. После установки откройте `/compass`.

BagSense доступен через общий репозиторий: установите его в `/xlplugins` и откройте `/bagsense`. Если использовали локальную DLL, сначала отключите копию в Dev Plugins.

VendorProfit 0.1.0: поиск лотов для перепродажи NPC, прибыль после налога и подсветка текущей выдачи. Команда `/vendorprofit`. Предварительная версия для проверки в игре; [описание и статус проверки](VendorProfit/README.md). Доступен через общую ленту, выпуск `vp-v0.1.0`.

## Установка через Dalamud

В `/xlsettings` → **Experimental → Custom Plugin Repositories** добавьте:

```text
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
```

Сохраните настройки, откройте `/xlplugins` и установите нужный плагин. Для русского интерфейса Event Tracker: Settings → Language → Русский.

Если уже подключён старый адрес `FFXIV-Ultrawide-Cutscenes`, оставьте существующую запись: она продолжает работать через перенаправление GitHub. Добавлять второй адрес для тех же плагинов не требуется. Не создавайте новый репозиторий под прежним именем — это отключит перенаправление.

## Структура

```text
UltrawideCutscenes/         проект кат-сцен, решение и документация
EorzeaEventTracker/         проект трекера, лента событий, переводы и тесты
EorzeaCompass/              навигатор по контенту, квестовые цепочки и тесты
BagSense/                   цели персонажа, заметки предметов и план очистки
tools/                     общие инструменты публикации
.github/workflows/         независимые сборки и обновление событий
pluginmaster.json          общая лента установки плагинов
build.ps1                  сборка одного или всех проектов
```

Каждый плагин выпускается независимо: теги `uc-v*` для Ultrawide Cutscenes, `eet-v*` для Event Tracker, `ec-v*` для Eorzea Compass и `bs-v*` для BagSense. Общий инструмент публикации обновляет соответствующую запись в `pluginmaster.json`, сохраняя остальные плагины.

## Сборка

Нужны .NET 10 SDK и установленные библиотеки Dalamud API 15. Для нестандартного расположения библиотек задайте `DALAMUD_HOME`.

Из корня репозитория:

```powershell
./build.ps1
./build.ps1 -Plugin UltrawideCutscenes
./build.ps1 -Plugin EorzeaEventTracker
./build.ps1 -Plugin EorzeaCompass
./build.ps1 -Plugin BagSense
```

Скрипт использует локальный `.dotnet`, если он есть, иначе системный SDK. Инструкции и ограничения каждого плагина находятся в его папке.

Данные новых событий Event Tracker обновляются через онлайн-ленту без пересборки DLL. [Формат ленты и добавление событий](EorzeaEventTracker/docs/ONLINE-FEED.md).

This repository contains independent Dalamud plugins. Add the shared custom repository URL above and install either plugin through `/xlplugins`. Plugin identities and saved settings remain unchanged by the repository rename.

Final Fantasy XIV belongs to Square Enix. This project is not affiliated with Square Enix or the Dalamud project.
