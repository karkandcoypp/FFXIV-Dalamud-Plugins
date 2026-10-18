Eorzea Event Tracker v0.3.0 for Dalamud API 15.

- Quest IDs, reward mappings, prices/currencies, event translations and artwork URLs now arrive through the online feed.
- New definitions apply without restarting or rebuilding the plugin. Current events have been migrated to the online format.
- Artwork loads asynchronously from official HTTPS image hosts and is cached locally. Existing bundled artwork remains a fallback.
- Invalid feed data preserves the last valid copy. Cached event details, mappings, translations and downloaded images work offline.
- Guaranteed quest reward claims are additionally checked against the game's reward list.
- New tracking mechanics and changes to the game API may still require a plugin update. New event details still need reviewed source annotations; they are not magically extracted from every Lodestone layout.

Install from the existing custom repository:
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Ultrawide-Cutscenes/main/pluginmaster.json

Open `/xlplugins`, find Eorzea Event Tracker, install it, then use `/events`.
Install this update once through `/xlplugins`. Subsequent event data refreshes on login, Refresh and every 30 minutes while logged in. This online catalog version still requires in-game verification.
