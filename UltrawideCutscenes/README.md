# FFXIV Ultrawide Cutscenes

A community-maintained Dalamud plugin that removes 16:9 letterboxing from Final Fantasy XIV cutscenes on ultrawide displays.

The original plugin is no longer actively maintained. This fork keeps it compatible with current versions of FFXIV and Dalamud.

## Features

- Removes black letterbox bars from in-engine cutscenes.
- Supports ultrawide and super-ultrawide displays.
- Can be enabled or disabled without restarting the game.
- Reports whether its native game hook initialized successfully.

## Installation

1. Open Dalamud settings with `/xlsettings`.
2. Select **Experimental**.
3. Add the following URL under **Custom Plugin Repositories**:

   ```text
   https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
   ```

4. Save the settings and open `/xlplugins`.
5. Search for **Ultrawide Cutscenes Community** and install it.

This plugin is distributed from the shared [FFXIV Dalamud Plugins repository](../README.md).

## Установка на русском

1. Откройте настройки Dalamud командой `/xlsettings`.
2. Перейдите в раздел **Experimental**.
3. Добавьте следующий адрес в список **Custom Plugin Repositories**:

   ```text
   https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
   ```

4. Сохраните настройки и откройте установщик плагинов командой `/xlplugins`.
5. Найдите **Ultrawide Cutscenes Community** и установите плагин.

Плагин убирает чёрные полосы формата 16:9 из внутриигровых кат-сцен на широкоформатных и сверхшироких мониторах.

## Commands

| Command | Description |
| --- | --- |
| `/pcutscenes` | Toggle the plugin on or off. |
| `/pcutscenes true` | Enable the plugin. |
| `/pcutscenes false` | Disable the plugin. |
| `/pcutscenes status` | Show the plugin and native hook status. |

## Compatibility

This version targets Dalamud API 15 and .NET 10. Native game signatures can change after an FFXIV update. If the plugin stops working, run `/pcutscenes status`; `native hook: not available` means the plugin requires an update for the current game client.

## Known limitations

Cutscenes are authored for a 16:9 frame. Removing the bars may expose content outside the intended camera area, including actors appearing early, unloaded objects, T-poses, or other staging artifacts.

## Building

Requirements:

- .NET 10 SDK
- A current Dalamud development installation

Build the solution in Release mode:

```powershell
dotnet build Dalamud.FullscreenCutscenes.sln --configuration Release
```

## Credits

- Original implementation by goat and Maple.
- Native letterbox discovery credited to aers.
- Community fork maintained by **Carcosa Lyuman**.

Final Fantasy XIV is a registered trademark of Square Enix Holdings Co., Ltd. This project is not affiliated with or endorsed by Square Enix or the Dalamud project.
