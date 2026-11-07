# Hunt Route

Custom Dalamud API 15 hunt-bill assistant, developed after studying SheepGoMeh/HuntBuddy. New runtime, routing solver, UI, teleport handling and reset tracking; bundled daily coordinates were extracted from HuntBuddy's location reference.

Command: `/huntroute`.

## Behavior

- Heavensward through Dawntrail expansion tabs, compact reset cards and contract/zone progress.
- Opaque compact layout down to 520x430. A zone dropdown replaces the sidebar at narrow widths. Contract cards and active-route controls remain above the scrolling target pane.
- The route joins remaining known daily target positions, grouping coincident locations and placing completed entries after them. Up to 18 distinct positions use an exact shortest open path; larger sets use bounded nearest-neighbor fallback. Outside the zone the entry is chosen from unlocked aetherytes; inside it the route starts at the character's position. Recalculation happens on contract, completion, territory/map or unlock changes, not continuously while moving.
- Clicking an unfinished daily target starts route following. Teleport occurs only if the destination territory differs from the current territory, uses the closest unlocked aetheryte and the normal gil cost. Ordinary target/start/board actions never request a same-zone teleport. A separate explicit Yes to a qualified long-flight suggestion is the only exception. Busy/loading/casting/pending/locked cases are rejected before the native call, with a short repeat-request guard. Hinterlands uses Idyllshire. Flight, inaccessible subregions and terrain are not modeled.
- Following waits for arrival, then sets a map flag without opening the map. Each completed target automatically flags the next remaining daily target in that zone. After the zone's daily goals it immediately opens a native game-style Yes/No confirmation, even if the main window is closed. Yes continues and No/close dismisses without repeated prompts; the main-window button remains available. Weekly B-ranks and unknown-position targets are excluded. Use Stop to pause; logout, character changes and replaced/discarded tracked contracts stop the session. Following is not resumed after plugin reload.
- Hover daily or weekly target names for the game's own hunt portrait, progress and coordinates. Portrait aspect ratio is preserved; asynchronous high-resolution loading falls back to normal-resolution assets.
- Route state survives BetweenAreas transitions, temporarily missing player objects and ContentId. A real logout or character change clears it. The first ready post-loading update restores the target flag, including same-territory teleports; a short bounded wait tolerates arriving bill-data snapshots and unavailable map agents.
- Long-flight suggestions are conservative: at least 1200 world metres to the target, at least 900 m less flight, at most 300 m from an unlocked destination aetheryte, cost at most 200 gil according to the current teleport list. They show actual distances and cost and require explicit Yes. Short flights, expensive or distant aetherytes do not produce an offer. The target's flag and following remain active after No.
- Confirmation dialogs use native Atk nodes and game-localized Yes/No labels via pinned KamiToolKit; the main hunt panel remains compact ImGui. Hunt Route owns its addon and does not replace or intercept the shared SelectYesno addon. Existing game confirmations defer our request. Target/price is validated again after Yes, and duplicate callbacks cannot execute twice. Native-dialog appearance and loading/cast behavior need in-game validation.
- Daily reset: 15:00 UTC (18:00 Moscow); weekly: Tuesday 08:00 UTC (11:00 Moscow). Countdown uses the computer's UTC clock.
- Contract status is reconstructed automatically from the game's obtained flags, obtained/available bill IDs and kill progress every second. Bills already held before plugin startup count immediately. Cards distinguish not taken, partially taken, taken, completed, and held bills with a newer set available on the board. Retained IDs without the obtained flag do not count as held bills. Reset countdowns are separate from the actual held-contract state; a repeated available ID alone is not proof that a held bill was acquired in the current reset period.
- Weekly search map displays possible **zone-level B-rank** locations from HuntHelper, with yellow candidate points and the player's blue point. These are community observations, not guaranteed, exhaustive or target-specific spawn predictions.
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
- HuntHelper B-rank candidate positions (MIT): https://github.com/img02/HuntHelper/tree/0c07d87a1e951f9dce7a17e500d41bbc752bee94
- Umbra was inspected as a behavior reference for client-visible world markers; no Umbra code was copied: https://github.com/una-xiv/umbra/tree/4f7abbd045e7769ec01ef6456dba0d72dee3c2da
- Reset schedule reference: https://ffxiv.consolegameswiki.com/wiki/Reset

HuntHelper's MIT notice is included as `HuntHelper-LICENSE.txt`. Native UI uses KamiToolKit (MIT), pinned at `b4b7cab18e3b60abb49391272acb421e35b52bad`; its source is vendored and `KamiToolKit-LICENSE.txt` is included in the package. See [third-party notices](THIRD-PARTY-NOTICES.md).
