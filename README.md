# FFXIV Dalamud Plugins

Общий репозиторий плагинов **Carcosa Lyuman** для FFXIV / Dalamud API 15.

| Плагин | Что делает | Команда | Исходники |
| --- | --- | --- | --- |
| Ultrawide Cutscenes Community | Убирает полосы 16:9 из внутриигровых кат-сцен на широких мониторах | `/pcutscenes` | [UltrawideCutscenes](UltrawideCutscenes/README.md) |
| Eorzea Event Tracker | Показывает текущие события, инструкции, награды и прогресс персонажа | `/events` | [EorzeaEventTracker](EorzeaEventTracker/README.md) |

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
tools/                     общие инструменты публикации
.github/workflows/         независимые сборки и обновление событий
pluginmaster.json          общая лента установки обоих плагинов
build.ps1                  сборка одного или обоих проектов
```

Каждый плагин выпускается независимо: теги `uc-v*` для Ultrawide Cutscenes и `eet-v*` для Event Tracker. Общий инструмент публикации обновляет соответствующую запись в `pluginmaster.json`, сохраняя другой плагин.

## Сборка

Нужны .NET 10 SDK и установленные библиотеки Dalamud API 15. Для нестандартного расположения библиотек задайте `DALAMUD_HOME`.

Из корня репозитория:

```powershell
./build.ps1
./build.ps1 -Plugin UltrawideCutscenes
./build.ps1 -Plugin EorzeaEventTracker
```

Скрипт использует локальный `.dotnet`, если он есть, иначе системный SDK. Инструкции и ограничения каждого плагина находятся в его папке.

Данные новых событий Event Tracker обновляются через онлайн-ленту без пересборки DLL. [Формат ленты и добавление событий](EorzeaEventTracker/docs/ONLINE-FEED.md).

This repository contains independent Dalamud plugins. Add the shared custom repository URL above and install either plugin through `/xlplugins`. Plugin identities and saved settings remain unchanged by the repository rename.

Final Fantasy XIV belongs to Square Enix. This project is not affiliated with Square Enix or the Dalamud project.
