# Hunt Route spatial audit (0.3.3)

Boards were checked against installed game `Level` objects, not approximate nearby NPC coordinates. World X/Z is converted using each map's size factor and offsets.

| Expansion | City | Aetheryte | Territory / map | Board Level row | EObj row | Map X / Y |
|---|---|---|---|---|---|---|
| Heavensward | Foundation | 70 | 418 / 218 | 5892509 | 2005909 | 12.718168 / 11.681350 |
| Stormblood | Kugane | 111 | 628 / 370 | 6928982 | 2008655 | 10.609598 / 10.362924 |
| Shadowbringers | The Crystarium | 133 | 819 / 497 | 7959481 | 2010340 | 9.568138 / 9.425624 |
| Endwalker | Old Sharlayan | 182 | 962 / 693 | 8950392 | 2012236 | 11.834738 / 13.205691 |
| Dawntrail | Tuliyollal | 216 | 1185 / 855 | 10577299 | 2014155 | 13.891332 / 13.685839 |

MapChecks verifies these objects, city names, board coordinates, bill tiers/expansions, daily coordinate coverage, map projections, production entry-point filtering and production route construction for first/middle/last contract sets. The same-zone regression includes a busy character with a profitable Amh Araeng shortcut: the dialog must remain queued until ready and must not repeat after No.

RouteChecks compares small random routes against exhaustive permutations and checks the larger-route fallback, the teleport eligibility matrix and same-zone arrival/completion state transitions. Its optional arguments are game sqpack path and random seed. Installed game validation also checks 77 weekly targets, 47 spawn maps and 923 possible B-rank points/textures.

Limits: local tests cannot prove exact live spawn placement of every daily mob or B-rank. Daily coordinates remain the bundled HuntBuddy reference; B-rank points are possible territory locations, not live sightings. Terrain, altitude, inaccessible subregions, real casting/loading and native addon rendering still need in-game validation. No game UI was controlled for this audit.
