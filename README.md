<div align="center">

# FFXIV Dalamud Plugins

Плагины **Carcosa Lyuman** для Final Fantasy XIV

[Каталог](#каталог) · [Установка](#установка) · [Сборка](#сборка) · [Релизы](https://github.com/karkandcoypp/xiv/releases)

</div>

## Каталог

| Плагин | Возможности | Команда |
| --- | --- | --- |
| [Hunt Route](HuntRoute/README.md) | Маршруты по контрактам, статусы досок, поиск элиты и предложения телепорта. Нативное меню с навигацией геймпадом. | `/huntroute` |
| [Eorzea Event Tracker](EorzeaEventTracker/README.md) | Календарь событий, инструкции, награды и прогресс персонажа. | `/events` |
| [Eorzea Compass](EorzeaCompass/README.md) | Журнал квестовых цепочек и атлас коллекций с источниками предметов и отметками прогресса. | `/compass` |
| [BagSense](BagSense/README.md) | Назначение предметов, квестовые резервы, личные цели и план очистки инвентаря. | `/bagsense` |
| [CraftProfile](CraftProfile/README.md) | Экспорт уровней профессий, экипировки и сохранённых комплектов для планирования крафта. | `/craftprofile` |
| [VendorProfit](VendorProfit/README.md) | Поиск лотов для перепродажи NPC и расчёт прибыли с учётом налога. | `/vendorprofit` |
| [Ultrawide Cutscenes Community](UltrawideCutscenes/README.md) | Убирает полосы 16:9 в игровых кат-сценах на широких мониторах. | `/pcutscenes` |

## Установка

1. В игре откройте `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Добавьте адрес и сохраните настройки.

   ```text
   https://raw.githubusercontent.com/karkandcoypp/xiv/main/pluginmaster.json
   ```

3. Откройте `/xlplugins`, найдите плагин по названию и установите его. Для открытия используйте команду из каталога.

> [!TIP]
> Один адрес обслуживает весь каталог. Если у вас уже подключён прежний адрес `FFXIV-Ultrawide-Cutscenes` и плагины видны в Dalamud, достаточно этой записи.

Если вы тестировали локальную DLL через **Dev Plugins**, отключите локальную копию перед установкой из каталога. Русский интерфейс Event Tracker включается в **Settings → Language → Русский**.

## Сборка

Понадобятся **.NET 10 SDK** и библиотеки **Dalamud API 15**. Если библиотеки лежат в нестандартной папке, задайте `DALAMUD_HOME`.

```powershell
./build.ps1
./build.ps1 -Plugin HuntRoute
./build.ps1 -Plugin VendorProfit
```

`build.ps1` использует локальный `.dotnet`, если он есть, или системный SDK. Команды для отдельных проектов и их тестов описаны в документации из каталога.

## Выпуски и данные

- Каждый плагин выпускается независимо. Версии и архивы доступны в [GitHub Releases](https://github.com/karkandcoypp/xiv/releases).
- [pluginmaster.json](pluginmaster.json) — общий индекс для установки и обновлений. При выпуске обновляется запись соответствующего плагина.
- Общие скрипты публикации находятся в [tools/](tools/), а автоматизация сборок — в [.github/workflows/](.github/workflows/).
- Event Tracker получает новые события без пересборки DLL. [Формат онлайн-ленты](EorzeaEventTracker/docs/ONLINE-FEED.md).

---

Final Fantasy XIV принадлежит Square Enix. Этот независимый репозиторий не связан с Square Enix или проектом Dalamud.
