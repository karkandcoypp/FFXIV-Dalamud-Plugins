# Hunt Route 0.2.0 audit

## Findings addressed

- Repeated native teleports were possible within the destination territory. The common teleport gate now returns before the native call whenever current and destination territories match. Tests exercise the actual wrapper with substituted services, including busy/locked/unready cases.
- Completed goals still shaped the route, and the starting point remained an aetheryte after entering the zone. Remaining distinct positions are now optimized from the player inside the map, and from unlocked entry points outside it. Completed entries remain available for display but do not affect the route.
- Coordinate math was duplicated for aetherytes and the elite-map texture. All projections now use the same sheet scale and offsets. The known Camp Cloudtop landmark and 47 map projections were checked along with raw map-link round trips.
- Bill IDs were not bounded by their order type. The game sheet's OrderAmount now bounds obtained/available IDs, preventing a corrupt/sentinel value from selecting another tier or implying a new set. Zero/invalid target rows are ignored.
- Missing MapMarker rows or invalid aetheryte references could interrupt the whole refresh. They now produce no entry candidates rather than an exception.
- Loading snapshots and replaced weekly targets could leave stale visible selections. Area-transition frames are not read, and weekly selection is invalidated/rebound when bill data changes. Player dots also require the selected map, not only the territory.
- More than 18 distinct targets could throw from the exact solver. A bounded deterministic fallback now handles such sets.
- Completed-target clicks now only open their location and do not teleport or start following.

## Following behavior

Following is scoped to the selected expansion's known daily targets. It waits for the destination territory, emits one flag, advances only after the target's required kills, and offers the next non-completed zone in displayed order, wrapping if necessary. It never issues a teleport from a progress update. A click is required for each zone change. Flags are retried when the map agent is temporarily unavailable, without opening the map on each update. Replaced/discarded contracts and character/logout changes terminate the session. Weekly search remains separate.

## Evidence and boundaries

- Build: API 15 / .NET 10, warnings treated as errors.
- RouteChecks: 119 checks covering exhaustive route comparison, resets, contract states, same-zone travel policy, completed/duplicate/unknown targets, bounded fallback and following-state transitions.
- MapChecks: 2806 installed-data/integration checks, including 1495 map-link round trips, 47 map projections, 794 portrait files, all board destinations and bill ranges. Native teleport is deliberately not invoked; the real wrapper is tested with substituted services.
- UiChecks: production ImGui rendering at 520x430, 620x500 (following and next-zone prompt), 800x620 and 1180x800. The compact screenshots were inspected visually.

Local data checks do not establish that every community-provided spawn coordinate is the current live spawn. The route minimizes geometric distance and does not model cliffs, flying restrictions or closed passages. Live FFXIV teleport/arrival behavior, automatic flags and game texture rendering are not covered by the local harness. Weekly positions remain zone-level community possibilities, not confirmed sightings.
