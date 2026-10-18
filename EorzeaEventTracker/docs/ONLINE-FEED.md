# Online event data (v0.3.0+)

One installation of plugin 0.3.0 enables data updates for new events. New event definitions do not require rebuilding the DLL. Changes to game APIs or genuinely new progress mechanics may still require a plugin update.

## Source and publication

Edit `config/overrides.json` under the event's official Lodestone URL. Add reviewed text, `tracking`, `imageUrl`, `translations` and `locations`. The collector merges these fields into `config/events.json`; do not only edit generated events.json, because the next collection will replace it.

Run from the EorzeaEventTracker directory:

```sh
python collector/collect.py
python -m unittest discover -s tests -v
```

The scheduled collector runs every six hours. It can also be dispatched manually. Feed-only edits do not need a release tag. The plugin downloads the feed on login, Refresh and every 30 minutes while logged in. Tracking definitions are applied on the framework thread at the next update; no plugin reload is needed.

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

The collector enriches supported official pages automatically as described below. Image-only text and primary completion objectives are not guessed. NPC destinations use exact game-data relationships; automatic Russian text is explicitly labelled. Reviewed overrides remain available for exceptions.

## Main event objective (v0.6.1)

Optional `tracking.completionQuestId` identifies the main quest used for automatic journal/widget visibility. It must refer to a quest in the same tracking definition; zero or omission means the main objective is unknown. Unknown completion is not treated as completed. The plugin waits for loaded character progress and the login feed refresh before deciding to auto-open.

Current mappings: All Saints' Wake — Grim and Grisly Games (71004); Fall Guys — Just Crowning Around (70337), as explicitly selected by the user. This is independent of collecting every reward or winning achievements. New event main quest IDs can be added through `config/overrides.json`, without a plugin rebuild.

Automatic display is suppressed if there are no visible active events with an uncompleted/unknown main objective. Manual journal/widget opening remains available. Favorite deadline reminders remain independent and can still notify about missing selected rewards after the main quest is complete.

## Favorite currency budget (0.14.0+)

Each tracked reward can specify `price`, `currency` (display label), and optional `currencyItemId` (game Item row ID, up to 1000000). Set the actual currency ID in reviewed overrides so future events can use the budget without a rebuild. Existing MGF entries without an ID remain compatible (MGF = 41629).

The journal sums one copy of each still-unconfirmed favorite, excluding ignored rewards, separately per currency. It subtracts the current character's live balance; owned/manual marks remove rewards from the cost. Zero-price entries without a quest are treated as unpriced and called out separately, not assumed free. Unavailable balances never imply zero currency or an exact deficit. MGF is read from CurrencyManager; item tokens can be read from inventory. Balances are refreshed with character scans and cleared on logout/character/feed change.

## Automatic enrichment

The collector discovers Lodestone announcements and follows a single official special-event link when possible, including sqex.to redirects checked against the official destination allowlist. If the special page has no current dates, the announcement supplies dates and the special page supplies reward details. Exactly two explicit, timezone-qualified timestamps are required. Known active/future events survive shortened discovery results and temporary source failures.

For new events without a reviewed tracking override:

- Official Eorzea Database links supply exact English item names. Only unique exact XIVAPI matches supply IDs and icons.
- Explicit quest headings/database links are resolved similarly. Up to eight unique direct continuations are followed, with no additional prerequisites. Guaranteed rewards are assigned only when they also appear on the official reward list. The main completion objective is not inferred.
- SpecialShop exchanges supply prices only when every matching offer agrees on one direct currency and amount, offers one normal-quality copy, and has no quest, achievement or collectability conditions. Truncated searches, indirect currencies, bundles, multiple costs, conflicting prices and competing GilShop listings stay unpriced. GilShop-only prices are currently unsupported.
- The official og:image supplies cover artwork from the approved CDN. Unknown/ambiguous facts are described in notes and automation.issues. The provenance includes source URLs and API version/schema, pinned during the run.

Automatically enriched events remain verified=false: identifying some facts does not establish complete coverage. Existing clients show the partial-details indicator; users who disabled partial/unverified events need to enable them to see these entries. Known facts remain usable for tracking and favorite currency budgets. Reasons for missing data are under How to join / Source notes. Missing prices are never treated as free.

Reviewed overrides take precedence, preserving existing maps, translations and event behavior. Temporary enrichment failures retain a known event's last details instead of erasing rewards. A wholly unusable run leaves the previous feed unchanged. Collection runtime and API requests are bounded; repeated requests are cached. No fuzzy item/NPC matches, OCR or speculative prices are published.

Each run writes collection-report.json beside the output. GitHub Actions uploads it as event-collection-report; it is diagnostic data, not plugin content. The workflow validates the feed with the client's own policy before committing.

This is a collector-only update: plugin 0.14.0 loads new data on Refresh, login or its periodic refresh without a DLL update. Automatic Russian translation and NPC destinations are handled as described below.

## Automatic Russian translation and NPC destinations

New automatically enriched events resolve quest issuers through Quest.IssuerStart + IssuerLocation -> Level -> Map. Level.Type must be ENpc (8), its object must match the quest issuer, and map/territory IDs must agree. NPC names come from ENpcResident; zone names come from Map.PlaceName. World X/Z are converted using the same SizeFactor/OffsetX/OffsetY formula as Dalamud MapLinkPayload, with finite/range checks.

Reward merchants follow item -> SpecialShop or GilShopItem -> ENpcBase.ENpcData -> Level. All supported shop links must identify one NPC, and the NPC must have one unique usable placement. Multiple NPCs, truncated results, multiple positions/floors, indirect shop handlers, mismatched IDs and unavailable rows leave the map button absent, with an explanation in source notes. No NPC is selected by name similarity. Guaranteed quest rewards link to that quest's issuer as a starting point, not to an inferred completion target. Locations and locationId use the existing feed format; no plugin update is needed.

Russian translations use the public MyMemory GET endpoint (https://api.mymemory.translated.net/get), en|ru. Only public event text is submitted; character data and plugin settings never enter the collector. Exact-source translations are accepted; fuzzy matches for different sentences, quota/error messages, markup, altered numbers or placeholders are rejected. Long text is split into bounded segments. Machine text is labelled in event notes; reviewed event translations always win.

config/translation-cache.json persists successful translations and UTC daily usage across scheduled runs. A consistent reviewed glossary seeds the cache; conflicting reviewed meanings stay event-scoped. The default budget is 1200 source characters per run and 4500 per UTC day. Untranslated strings are retried on later scheduled runs; service failure/quota exhaustion leaves original English and previously cached Russian usable. The cache is committed together with the feed only after client validation. No service key is required. The public service has its own availability/limits, so a new event may initially have partial Russian coverage.

Source references: https://v2.xivapi.com/docs/guides/sheets/ ; https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/Text/SeStringHandling/Payloads/MapLinkPayload.cs ; https://mymemory.translated.net/doc/spec.php .

## Entrance routes for inaccessible maps

A valid Map/Level row does not guarantee that the game can open that map remotely. The in-game test confirmed that the Blunderville Square merchant map cannot be opened from outside, although its coordinates and IDs are valid. Quest map links to the Gold Saucer did work.

config/map-routes.json is a shared territory-level entrance registry, independent of event IDs, item IDs and plugin code. Its Blunderville entry is supported by the official patch 6.51 notes: speak with Wy in the Gold Saucer (X:6.1 Y:7.4). The collector resolves the entrance NPC's unique position from game data and verifies the expected territory/map; it does not guess a route from the event's quest list.

Routing is applied AFTER reviewed overrides, so curated events and newly collected events use the same correction. The published location itself points to the entrance, making it compatible with existing 0.14.0 clients. Its NPC label and source notes explain the entrance and retain the final merchant/area coordinates. Optional navigation metadata records the original destination for provenance. Unknown/unconfirmed entrance coordinates remove the broken button instead of publishing the inaccessible destination. Other zones are unchanged; unknown inaccessible areas still require a confirmed registry route. New registry entries need no DLL update.

The isolated Fall Guys test feed was corrected through the same routing stage. Its original no-manual-data collection provenance is retained, with an explicit navigation-correction annotation. This correction does not turn the earlier data-only validation into an in-game map-click test.

## English item/NPC names (0.15.0)

Item names and NPC labels always remain English, independently of the interface language. Localizer enforces this for tracked reward names and event NPC labels even if an older/custom feed includes Russian aliases. Existing aliases are also restored to English when encountered inside translated descriptions. Quest titles, event titles, interface labels, zones and descriptions keep their language behavior.

The collector excludes item and NPC names from translation requests and publishes identity translations for these names so older 0.14.0 clients also keep English names. Previously cached/online aliases are normalized in event text where they match known names. This policy includes the original merchant NPC in entrance-route metadata. Names retain their source IDs and strings; tracking/favorite/manual state is unaffected.
