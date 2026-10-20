# BagSense 0.2.0 validation

21 October 2026, Windows, .NET SDK 10.0.402 and installed Dalamud API 15 libraries.

## Local checks

- Release build treats warnings as errors.
- Policy checks cover reservations, stack projections, character isolation, localization, NPC selection, complete prices, saved routes, legacy-plan compatibility and locale-independent keys.
- Newtonsoft round trips cover the actual Configuration class, goals, notes, settings, plans and selected NPC/reward keys.
- Installed-game checks validate indices, semantic quest inputs, complete prices, NPC shop links and map/territory coordinates.
- Publisher/package tests cover preserving other plugins, replacing BagSense, matching tags, checksum/ZIP contents and download links.

Observed coverage: 6,743 merchant-listed items, 3,404 recipe ingredients, 3,385 direct exchange input items, 973 shops linked to valid NPC map targets and 132 ordinary merchant locations. Catalog construction took approximately 0.83 seconds. These counts do not imply every access condition or journal step is solved.

## Specific data regressions

- Copper Ore is requested by **My First Pickaxe** (quest 66135), with a mapped contact. `ITEM0` event-item IDs and unrelated `LOC_ITEM` constants are not ordinary quest inputs.
- Aetherpool Grip (23165) reaches **E-Una-Kotor** through `DEEP_DUNGEON1_WEAPON_SHOP`.
- Aloalo Potsherd (41078) reaches **Trisassant**, retaining access quest 70325.
- Aetherspun Gold (47866) reaches **expedition armorer** (1053612), South Horn (territory 1252, map 967). Upgrade prices include the base gear, three gold and six X-Fixative.
- The armorer is absent from Level. Placement comes from `bg/ex5/03_ocn_o6/btl/o6b1/level/planevent.lgb`, ENPC BaseId 1053612, world coordinates approximately X 810.7881, Z -717.7692. Map coordinates use the installed map size and offsets.

## UI evidence

Production BagWindow/theme rendered in a standalone ImGui context: RU/EN, three tabs, 860×570 / 1120×760 / 1440×1000 with enlarged text; installed-data cards for Aetherspun Gold, Aetherpool Grip, Aloalo Potsherd, Copper Ore and Allagan Tin Piece. Character quantities and quest completion are sample values, not observations of the user's character.

Text/number fields are inert substitutes because Dalamud InputText requires its host. Game icons are omitted. Standalone fonts can differ from FFXIV. Rendering does not test typing, context menus or native map clicks. Detail scrolling is intentional.

## In-game checks still required

1. Update through `/xlplugins` and open `/bagsense`.
2. Check the screenshot's gold, grip and potsherd: purpose, complete price, NPC/menu and access quest status.
3. Click **Open map** and confirm the marker. The South Horn instance map may require content access.
4. Select a reward/NPC, add a step, reload the plugin, and confirm the same destination and price.
5. Check existing goals/notes/plans, character changes and RU/EN.

Publication is not in-game verification. Published ZIPs have a SHA-256 sidecar. Local builds use Offline only when the NuGet audit endpoint is inaccessible; GitHub builds retain the online audit.
