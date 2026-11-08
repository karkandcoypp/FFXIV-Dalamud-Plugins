# Hunt Route

Custom Dalamud API 15 hunt-bill assistant, developed after studying SheepGoMeh/HuntBuddy. New runtime, routing solver, UI, teleport handling and reset tracking; bundled daily coordinates were extracted from HuntBuddy's location reference.

Command: `/huntroute`.

## Behavior

- Heavensward through Dawntrail expansion tabs, compact reset cards and contract/zone progress.
- Slate/lavender compact layout down to 520x430, mint completion checks, segmented progress and green completed-expansion labels. Expansion tabs become a dropdown when measured text would not fit. A zone dropdown replaces the sidebar at narrow widths. Contract cards and active-route controls remain above the scrolling target pane.
- The route joins remaining known daily target positions, grouping coincident locations and placing completed entries after them. Up to 18 distinct positions use an exact shortest open path; larger sets use bounded nearest-neighbor fallback. Outside the zone the entry is chosen from unlocked aetherytes; inside it the route starts at the character's position. Recalculation happens on contract, completion, territory/map or unlock changes, not continuously while moving.
- Clicking an unfinished daily target starts route following. Teleport occurs only if the destination territory differs from the current territory, uses the closest unlocked aetheryte and the normal gil cost. Ordinary target/start/board actions never request a same-zone teleport. A separate explicit Yes to a qualified long-flight suggestion is the only exception. Busy/loading/casting/pending/locked cases are rejected before the native call, with a short repeat-request guard. Hinterlands uses Idyllshire. Flight, inaccessible subregions and terrain are not modeled.
- Following waits for arrival, then sets a map flag without opening the map. Each completed target automatically flags the next remaining daily target in that zone. After the zone's daily goals it immediately opens a native game-style Yes/No confirmation, even if the main window is closed. Yes continues and No/close dismisses without repeated prompts; the main-window button remains available. Weekly B-ranks and unknown-position targets are excluded. Use Stop to pause; logout, character changes and replaced/discarded tracked contracts stop the session. Following is not resumed after plugin reload.
- Hover daily or weekly target names for the game's own hunt portrait, progress and coordinates. Portrait aspect ratio is preserved; asynchronous high-resolution loading falls back to normal-resolution assets.
- Route state survives BetweenAreas transitions, temporarily missing player objects and ContentId. A real logout or character change clears it. The first ready post-loading update restores the target flag, including same-territory teleports; a short bounded wait tolerates arriving bill-data snapshots and unavailable map agents.
- Cheap intra-zone suggestions compare estimated flight time at 20 world m/s with teleport overhead plus the final flight. Require direct flight of at least 30 s, savings of at least 10 s and 20%, and cost at most 200 gil. Initial overhead is 18 s; completed plugin teleports update the session estimate to observed request-to-ready time plus 3 s for remounting. Estimates use horizontal distances, without terrain or altitude. The dialog shows times and savings and requires explicit Yes. No keeps following.
- Confirmation dialogs use native Atk nodes via pinned KamiToolKit, with slate panels, a destination heading, a target card and explicit `Телепортироваться` / `Позже` buttons; the main hunt panel remains compact ImGui. Hunt Route owns its addon and does not replace or intercept the shared SelectYesno addon. Existing game confirmations defer our request. Target/price is validated again after Yes, and duplicate callbacks cannot execute twice. Native-dialog appearance and loading/cast behavior need in-game validation.
- Completion history is saved atomically to `completion-history.json` inside the plugin config directory, using ContentId and bill tier (independent characters and expansions). It survives game/plugin restarts. Daily checks expire at 15:00 UTC and weekly checks Tuesday 08:00 UTC, regardless of retained client counters. First-time import accepts current-set completion from the game; subsequent resets require new evidence and cannot resurrect yesterday's check from stale counters. Fresh pickup handles repeated set IDs. Failed saves retain the previous file and retry; logout/unload makes a final attempt.
- Completed bills clear the held flag, but retained order IDs and kill counters are still checked against all required kills. Current available-set IDs distinguish completion from an old set. Fully completed daily sets show a green check, `Выполнено сегодня` and a `3/3` counter; an empty route explains that daily hunting is done. New available sets clear this check; discarded partial bills never count as complete.
- Daily reset: 15:00 UTC (18:00 Moscow); weekly: Tuesday 08:00 UTC (11:00 Moscow). Countdown uses the computer's UTC clock.
- Contract status is reconstructed automatically from the game's obtained flags, obtained/available bill IDs and kill progress every second. Bills already held before plugin startup count immediately. Cards distinguish not taken, partially taken, taken, completed, and held bills with a newer set available on the board. Retained IDs without the obtained flag do not count as held bills. Reset countdowns are separate from the actual held-contract state; a repeated available ID alone is not proof that a held bill was acquired in the current reset period.
- Weekly search map displays possible **target-specific B-rank** locations from ffxiv-huntmaps-maker: 77 named targets, 47 zones and 922 point entries across their sets. The selected target ID, territory and map must all match. Unknown targets do not fall back to another mob's points. Yellow candidate points are community observations, not live sightings or a guarantee of exhaustive coverage.
- Click a candidate to flag it on the native map. Native temporary marks are paged in groups of 12, matching the client limit. Only an explicit button writes these temporary map markers.
- World overlay labels only living, unfinished weekly targets present in the local object table, default range 300 m (adjustable 50-500 m). It cannot see mobs outside the client's loaded range. The overlay hides during cutscenes/loading.
- ARR contract data is read but does not have a dedicated board tab in this initial version.

## Validation

`dotnet run --project HuntRoute/tests/RouteChecks -c Release`

For installed-game validation, run the same executable with the game's `game/sqpack` path while the working directory is the repository's parent. The optional check validates every weekly target's territory, spawn coordinates and map texture paths.

`dotnet build HuntRoute/HuntRoute/HuntRoute.csproj -c Release --warnaserror`

Optional coordinate regression checks against installed game data:

`dotnet run --project HuntRoute/tests/MapChecks -c Release -- "<game/sqpack>" HuntRoute/data/elite-spawns.json`

Map flags, aetheryte positions and the elite-map overlay share the same scale/offset conversion. MapChecks validates raw links, texture projections, board destinations, available game portraits, bill ranges and actual teleport/map wrappers on substituted services. It does not call the native teleport. See [audit findings](AUDIT-0.2.0.md).

Local checks do not verify actual game teleport, overlay, board interaction or map-marker rendering.

## Sources

- HuntBuddy coordinate reference: https://github.com/SheepGoMeh/HuntBuddy/tree/1de61e25b99b6473f4d2c75b4023e588016f0aa4
- Current target-specific B-rank positions (MIT): https://github.com/RKI027/ffxiv-huntmaps-maker/tree/047ac3fddf28eb3232c59ed7006d085584963a9d
- Historical zone-wide candidate positions through 0.3.3 (MIT): https://github.com/img02/HuntHelper/tree/0c07d87a1e951f9dce7a17e500d41bbc752bee94
- Umbra was inspected as a behavior reference for client-visible world markers; no Umbra code was copied: https://github.com/una-xiv/umbra/tree/4f7abbd045e7769ec01ef6456dba0d72dee3c2da
- Reset schedule reference: https://ffxiv.consolegameswiki.com/wiki/Reset

The target-specific huntmaps MIT notice is included as `Huntmaps-LICENSE.txt`; the earlier HuntHelper notice is retained as `HuntHelper-LICENSE.txt`. Native UI uses KamiToolKit (MIT), pinned at `b4b7cab18e3b60abb49391272acb421e35b52bad`; its source is vendored and `KamiToolKit-LICENSE.txt` is included in the package. See [third-party notices](THIRD-PARTY-NOTICES.md).

Coordinate review, source revisions and limitations: [COORDINATE-AUDIT.md](COORDINATE-AUDIT.md). Verify the audited snapshot with `python HuntRoute/tools/verify_coordinate_data.py`; optionally pass `--huntmaps <upstream data/marks.json>` to compare all target assignments to the pinned source.

Travel confirmations share a measured layout: native text measurement sizes the real game addon; offline composition previews cover next-zone, intra-zone and long-name cases. Intra-zone offers display quoted gil cost and estimated direct/teleport/saved time in separate cards. The game frame/button textures may vary with the selected game theme; offline previews are not live Atk screenshots.

### Weekly route search
Weekly B-ranks follow daily targets and block zone departure until contract completion. The route and world overlay share a live contract-elite detector. Fly-by detection runs every 100 ms while the route is active, separately from the 1-second contract snapshots. Within 45 horizontal yalms of an undetected spawn candidate, the next point is flagged immediately, including in flight. No landing, hovering, dwell timer, stable-motion check or manual confirmation is required. Detection always wins over point advancement; found elites stay locked to their latest observed position through lost visibility/loading until contract completion. Combat/casting pause speculative advancement. Only client-loaded objects can be detected: absence is not proof that the whole area is empty. After all candidates were visited, a new round starts automatically. The previous point is deferred when other candidates exist; a nearby repeated point must be exited and re-entered before another visit is counted, so stationary players do not churn through rounds. Hover any mob name for its in-game hunt portrait.
