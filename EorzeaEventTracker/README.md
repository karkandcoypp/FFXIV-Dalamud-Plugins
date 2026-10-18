# Eorzea Event Tracker

Dalamud API 15 plugin for official FFXIV event reminders. Open `/events` for an illustrated journal with event artwork, item icons, reward cards, quest completion, character reward progress, local deadlines, participation steps and prices.

## Install in Dalamud

Add this URL to `/xlsettings` → Experimental → Custom Plugin Repositories:

```text
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Ultrawide-Cutscenes/main/pluginmaster.json
```

Save, open `/xlplugins`, search for **Eorzea Event Tracker**, and install it. Disable any development copy first. See [Russian instructions](INSTALL-RU.md).

Version 0.2.0 maps 4 quests and 31 rewards across All Saints' Wake and Fall Guys as of October 18, 2026. Sources: [All Saints' Wake](https://na.finalfantasyxiv.com/lodestone/special/2026/All_Saints_Wake/tfrx2rkv52), [official reward showcase](https://na.finalfantasyxiv.com/blog/003906.html), [Fall Guys rules and prize table](https://na.finalfantasyxiv.com/lodestone/special/fallguys/).

## Automatic character progress

Quest completion and collection unlocks use Dalamud 15's managed services. Loaded bag, equipped and armory containers are scanned once per second. A five-second grace period follows character changes; progress clears on logout. No native hooks or direct memory access are used by this plugin.

- **Unlocked**: a collectible is registered (mount, minion, emote, orchestrion, framer's kit, where supported by the game).
- **In bags / equipped**: an item is currently found in the scanned containers, including armory.
- **Earned from quest**: a guaranteed item reward from a completed quest, even if the item was moved or discarded.
- **Previously obtained**: the plugin observed the item before on this character.
- **Not unlocked**: a supported collection unlock is not registered.
- **Not detected**: no evidence is available; this does not mean the item was never owned.

Retainers, housing, glamour dresser and armoire are not scanned. Earlier purchases of non-collectible items cannot always be reconstructed. Observations are saved locally under the current character's content ID; they are never sent to the feed host. Existing unlocks from earlier event iterations count. Quest completion and all-rewards collection are separate.

`config/tracking.json` is a bundled, versioned catalog, independent of the online announcement feed. `tools/build_tracking.py` resolves exact English quest and item IDs from XIVAPI game data and confirms guaranteed quest rewards; run it explicitly when maintaining mappings. Art sources are recorded in `assets/SOURCES.md`.

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

Local verification: Release build with no warnings/errors, Python collector/index/catalog tests, C# feed/progress policy checks and inventory-container API checks. Version 0.1.2 was confirmed working in-game by the user. New tracking and UI in 0.2.0 still require in-game testing.
