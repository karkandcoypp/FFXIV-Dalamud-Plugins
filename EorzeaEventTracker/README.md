# Eorzea Event Tracker

Dalamud API 15 plugin for official FFXIV event reminders. Open `/events` for an illustrated journal with event artwork, item icons, reward cards, quest completion, character reward progress, local deadlines, participation steps and prices.

## Install in Dalamud

Add this URL to `/xlsettings` → Experimental → Custom Plugin Repositories:

```text
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
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

Version 0.3.0 reads quest/reward mappings from each event in the online feed and applies changes without reloading the plugin. `config/tracking.json` is a bundled backup for legacy feeds. `tools/build_tracking.py` can resolve game IDs when maintaining mappings; the reviewed online definitions live in `config/overrides.json`. Art sources are recorded in `assets/SOURCES.md`.

## Language

Settings → Language → English / Русский applies immediately and persists. UI, statuses, countdowns, login messages and current event guides are translated. Dates follow the chosen language and local time zone. New event translations load from the feed; missing translations fall back to source text. Quest/item names stay English unless a reviewed event translation supplies a name; original item names remain in tooltips.

## Event data

The bundled snapshot works offline. The hosted HTTPS feed is enabled by default; login, Refresh and a 30-minute timer while logged in update the local cache. Clear Settings → HTTPS feed URL for local mode. In local mode Refresh reloads the cache, falling back to bundled data. A cache file in the plugin configuration directory takes priority over bundled data.

New event announcements, instructions, quest/reward mappings, artwork and translations do not require rebuilding the plugin. The collector publishes reviewed annotations from `config/overrides.json`; completely automatic extraction of all event details is not promised. A genuinely new tracking mechanic or a game API change may still need a plugin update. See [online feed format and maintenance](docs/ONLINE-FEED.md).

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

Local verification covers Release compilation, collector/index/catalog/localization tests, online-only event ingestion, offline persistence, image caching/validation, progress rules and inventory-container API checks. Version 0.2.0 was confirmed working in-game by the user. The 0.3.0 online catalog still requires in-game testing.

## Favorite reward deadline reminders (v0.5.0)

Mark desired rewards as Favorite. While logged in, the plugin checks active events every minute and shows a Dalamud popup plus a local chat message within 72 hours and within 24 hours of the deadline if favorite rewards remain unconfirmed on this character. No reminders are sent for unknown or unloaded reward data, or for completed/expired events.

Each threshold is delivered once per character, event and deadline, and delivery history survives restarts. A login during the final day receives only the final-day reminder. If the official deadline changes, its new reminder history is independent. Reminders require the game/plugin to be running; an offline threshold is caught up after login while the event remains active. Settings has a separate reminder toggle, enabled by default. This is independent of the general login announcement setting.

## Compact widget (v0.6.0)

Use `/eventswidget` or `/events widget` to toggle a separate movable, resizable window. You can also enable Show compact event widget in Settings. The choice persists across restarts and the widget hides while logged out. It starts hidden for existing installations.

The widget lists active events from the current feed, ordered by deadline, with time remaining and confirmed-reward progress for the logged-in character. It follows the journal's partial-event visibility setting. Loading data and events without tracking are labeled explicitly. Click an event title to open its full journal; the Journal button opens the main window.

## Unwanted rewards (v0.7.0)

Mark Not interested on a reward card to mute it and exclude it from reward progress in every view. Both the obtained count and the total count exclude marked rewards. The remaining-rewards filter also omits them. Disable the remaining/favorites filters to see and restore excluded cards.

Ignoring a favorite removes it from favorites and reminders; favoriting an ignored reward restores it. Clearing Not interested restores inclusion without erasing acquisition history. These local preferences are shared between characters and persist across restarts. Main-quest completion and automatic opening rules are unchanged.
