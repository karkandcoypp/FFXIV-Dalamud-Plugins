# Hunt Route 0.1.2

- Fix map flags clustering near the map centre: displayed map coordinates are now converted to world coordinates before creating raw map links. Applies to daily targets, hunt boards and individual B-rank spawn points.
- Determine contract status directly from game snapshots, including bills acquired before plugin startup. Remove the dependency on locally observed pickup events.
- Show not taken, partially taken, taken, completed and held bills with a newer available set. A retained bill ID without its obtained flag is not treated as an active contract.
- Add regression checks for startup/partial/completed/older contracts and raw coordinate round trips using installed game sheets.

Open `/huntroute`. Local validation covered 1404 raw map-link round trips; actual game map flags and acquisition transitions still need an in-game check.
