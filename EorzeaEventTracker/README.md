# Eorzea Event Tracker

Dalamud API 15 plugin for official FFXIV event reminders. Open `/events` to view active and upcoming events, local dates, requirements, participation steps and rewards. Completion checkboxes are manual.

## Install in Dalamud

Add this URL to `/xlsettings` → Experimental → Custom Plugin Repositories:

```text
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Ultrawide-Cutscenes/main/pluginmaster.json
```

Save, open `/xlplugins`, search for **Eorzea Event Tracker**, and install it. Disable any development copy first. See [Russian instructions](INSTALL-RU.md).

Version 0.1.2 includes verified participation details and rewards for All Saints' Wake and Fall Guys as of October 18, 2026. Sources: [All Saints' Wake](https://na.finalfantasyxiv.com/lodestone/special/2026/All_Saints_Wake/tfrx2rkv52), [official reward showcase](https://na.finalfantasyxiv.com/blog/003906.html), [Fall Guys rules and prize table](https://na.finalfantasyxiv.com/lodestone/special/fallguys/).

## Event data

The bundled snapshot works offline. The hosted HTTPS feed is enabled by default; login and Refresh update the local cache. Clear Settings → HTTPS feed URL for local mode. In local mode Refresh reloads the cache, falling back to bundled data. A cache file in the plugin configuration directory takes priority over bundled data.

The collector discovers official NA Lodestone articles and includes pages pinned in `config/overrides.json`. It requires explicit Pacific dates and skips uncertain ranges. Rewards and steps are reviewed annotations; future events may contain partial data. A scheduled GitHub Action refreshes the feed twice daily. Network errors fail the collection without overwriting the previous feed.

From this directory:

```powershell
py -m pip install -r collector/requirements.txt
py -m unittest discover -s tests -v
py collector/collect.py
```

## Build and test

Requires .NET 10 and Dalamud development files from XIVLauncher (API 15). For a custom location set `DALAMUD_HOME`.

From this directory:

```powershell
./build.ps1
dotnet run --project tests/FeedChecks/FeedChecks.csproj -c Release
```

Output: `EorzeaEventTracker/bin/Release/EorzeaEventTracker.dll`. Complete Dalamud ZIP: `EorzeaEventTracker/bin/Release/EorzeaEventTracker/latest.zip`.

The root GitHub Actions release workflow builds and tests `eet-v*` tags, publishes a release ZIP, then merges the plugin into `pluginmaster.json`, retaining the Ultrawide entry. Tags match assembly versions without the final `.0` (for example `eet-v0.1.2`).

Local verification: Release build with no warnings/errors, Python collector/index tests, and C# feed regression checks. In-game behavior still requires user testing. Automatic quest/inventory/reward progress is not implemented.
