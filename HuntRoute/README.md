# Hunt Route

Custom Dalamud API 15 hunt-bill assistant, developed after studying SheepGoMeh/HuntBuddy. New runtime, routing solver, UI, teleport handling and reset tracking; bundled daily coordinates were extracted from HuntBuddy's location reference.

Command: `/huntroute`.

## Behavior

- Heavensward through Dawntrail expansion tabs, compact reset cards and contract/zone progress.
- Opaque dark layout: choose a zone in the left pane and follow its route in the right pane. Daily target rows have explicit teleport/map buttons; weekly B-rank search is shown separately.
- Exact shortest open path through known daily targets in a territory, choosing the entry aetheryte from the character's unlocked list. Route order stays stable as targets are completed.
- Clicking a target teleports to the closest unlocked aetheryte for that target, using the game's normal gil cost, and opens a map flag. Hinterlands uses Idyllshire. Flight, inaccessible subregions and terrain are not modeled.
- Daily reset: 15:00 UTC (18:00 Moscow); weekly: Tuesday 08:00 UTC (11:00 Moscow). Countdown uses the computer's UTC clock.
- Collection state is persisted per content ID and bill. Only observed acquisition transitions are recorded; stale retained IDs do not imply current-period collection. At first use or after taking bills while the plugin was inactive, check the board to resolve uncertain status. The dashboard distinguishes the schedule from an observed pickup.
- Weekly search map displays possible **zone-level B-rank** locations from HuntHelper, with yellow candidate points and the player's blue point. These are community observations, not guaranteed, exhaustive or target-specific spawn predictions.
- Click a candidate to flag it on the native map. Native temporary marks are paged in groups of 12, matching the client limit. Only an explicit button writes these temporary map markers.
- World overlay labels only living, unfinished weekly targets present in the local object table, default range 300 m (adjustable 50-500 m). It cannot see mobs outside the client's loaded range. The overlay hides during cutscenes/loading.
- ARR contract data is read but does not have a dedicated board tab in this initial version.

## Validation

`dotnet run --project HuntRoute/tests/RouteChecks -c Release`

For installed-game validation, run the same executable with the game's `game/sqpack` path while the working directory is the repository's parent. The optional check validates every weekly target's territory, spawn coordinates and map texture paths.

`dotnet build HuntRoute/HuntRoute/HuntRoute.csproj -c Release --warnaserror`

Local checks do not verify actual game teleport, overlay, board interaction or map-marker rendering.

## Sources

- HuntBuddy coordinate reference: https://github.com/SheepGoMeh/HuntBuddy/tree/1de61e25b99b6473f4d2c75b4023e588016f0aa4
- HuntHelper B-rank candidate positions (MIT): https://github.com/img02/HuntHelper/tree/0c07d87a1e951f9dce7a17e500d41bbc752bee94
- Umbra was inspected as a behavior reference for client-visible world markers; no Umbra code was copied: https://github.com/una-xiv/umbra/tree/4f7abbd045e7769ec01ef6456dba0d72dee3c2da
- Reset schedule reference: https://ffxiv.consolegameswiki.com/wiki/Reset

HuntHelper's MIT notice is included in the plugin package as `HuntHelper-LICENSE.txt`.
