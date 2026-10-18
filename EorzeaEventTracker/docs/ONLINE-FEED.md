# Online event data (v0.3.0+)

One installation of plugin 0.3.0 enables data updates for new events. New event definitions do not require rebuilding the DLL. Changes to game APIs or genuinely new progress mechanics may still require a plugin update.

## Source and publication

Edit `config/overrides.json` under the event's official Lodestone URL. Add reviewed text, `tracking`, `imageUrl`, `translations` and `locations`. The collector merges these fields into `config/events.json`; do not only edit generated events.json, because the next collection will replace it.

Run from the EorzeaEventTracker directory:

```sh
python collector/collect.py
python -m unittest discover -s tests -v
```

The scheduled collector runs twice daily. It can also be dispatched manually. Feed-only edits do not need a release tag. The plugin downloads the feed on login, Refresh and every 30 minutes while logged in. Tracking definitions are applied on the framework thread at the next update; no plugin reload is needed.

Existing plugins before 0.3.0 ignore the new optional fields. `schemaVersion` remains 1. Legacy feeds without them still work with the bundled fallback definitions.

## Fields

Every event retains `id`, `title`, `url`, `startsAt`, `endsAt`, `description`, `requirements`, `steps`, `rewards`, `notes`, `verified`.

Optional extensions:

```json
{
  "imageUrl": "https://lds-img.finalfantasyxiv.com/h/2/S6cUfbRM1Huta2gcz90SFzZhFM.jpg",
  "tracking": {
    "eventId": "692a7561de5dd384",
    "quests": [{"id": 71004, "name": "Grim and Grisly Games"}],
    "rewards": [{
      "id": 52402,
      "name": "Grisly Bear Head",
      "icon": 56959,
      "price": 0,
      "currency": "MGF",
      "earnedByQuest": 71004
    }]
  },
  "translations": {
    "ru": {
      "All Saints' Wake 2026": "День всех святых — 2026",
      "Grisly Bear Head": "Голова медведя"
    }
  }
}
```

This is a shortened real example, not an additional live event. Event IDs are the first 16 hex characters of SHA-256 of the official URL; `tracking.eventId` must equal the enclosing event ID.

- Quest IDs are full Quest sheet IDs, e.g. 71004, not the reduced 16-bit ID. Item IDs and icons come from the game sheets. A missing row stays unknown. Use exact English names when resolving IDs with XIVAPI.
- `earnedByQuest` is optional and must refer to a quest in this definition. The client additionally verifies the game's guaranteed reward list; it does not trust a feed claim alone. Optional/selectable quest prizes must not be marked guaranteed.
- `price` is optional; zero hides it. `currency` defaults to MGF and can contain the appropriate currency name for a new event.
- Russian and English translation dictionaries are scoped to each event and override the embedded fallback. Keys are the exact source text, including punctuation. Include titles, descriptions, requirements, steps, reward-guide strings and notes. Quest and reward names may also be translated; the original item name remains in its tooltip.
- No matching translation means the source text is displayed. Interface labels still use the built-in language resources.
- `tracking` can be omitted for a basic announcement or set to an empty definition to override a previous mapping. `config/tracking.json` is only an offline/legacy backup, not the primary online catalog.

## Artwork and offline behavior

### NPC map links (v0.4.0+)

An optional event `locations` array supplies NPC map destinations. Quest and reward entries reference a destination using optional `locationId`. Missing locations leave map buttons hidden; no location is guessed. New destinations can be published through the feed without a DLL release.

```json
{
  "locations": [
    {"id":"mgf-trader", "npc":"MGF Trader", "zone":"Blunderville Square", "territoryId":1197, "mapId":891, "x":3.8, "y":3.1}
  ]
}
```

Use displayed map coordinates, not world coordinates. Territory and map IDs must match the installed game's Map/TerritoryType sheets; the client checks this before opening a map. The action only opens the map with a flag, and is disabled when logged out. NPC and zone names use event translations. Up to 32 destinations per event are accepted; invalid coordinates, duplicate IDs and unknown references reject the feed and retain the last valid copy. Cached destinations remain usable offline.

Initial destinations were checked against the installed game sheets (quest issuer NPC/Level, Map and TerritoryType). MGF trader coordinates are published on the [official Fall Guys page](https://na.finalfantasyxiv.com/lodestone/special/fallguys/). The seasonal peddler location is [Old Gridania X:10.3 Y:9.0](https://ffxiv.consolegameswiki.com/wiki/All_Saints%27_Wake_%282026%29).

Favorites are stored locally by item ID in plugin configuration, shared between characters. They are independent of event feed contents and each character's obtained-reward history.

Images are loaded asynchronously only when an event is viewed, from HTTPS URLs on `lds-img.finalfantasyxiv.com` or `img.finalfantasyxiv.com`. Redirects to other hosts are not followed. PNG/JPEG images are capped at 4 MB, 8192 pixels per dimension and 16 million pixels overall, with at most two concurrent downloads.

The cache uses a hash of the URL, never a path supplied by the feed. Change the URL when replacing an image; official CDN image URLs are normally content-specific. Refresh retries failed downloads. Previously downloaded images remain usable offline; existing bundled artwork is a fallback for the original two events. A never-viewed new event may have no image while offline.

Invalid or unsupported feed data leaves the last valid feed in use. No scripts, assemblies, hooks or executable expressions can be delivered through this format. Only known game-sheet lookups and existing tracking rules are applied.

New content is not automatically inferred with full accuracy from arbitrary Lodestone layouts. Event discovery and date parsing are automatic where supported; quest/reward IDs, translations and participation details still require reviewed annotations in the feed source.
