# Coordinate review: Hunt Route 0.3.4

Checked 2026-11-07 against current fetched upstream versions. HuntBuddy HEAD remains `1de61e25b99b6473f4d2c75b4023e588016f0aa4`; HuntHelper HEAD remains `0c07d87a1e951f9dce7a17e500d41bbc752bee94`.

## Daily mobs

All 476 supported daily targets matched the independently published XIVDaily HW/SB/ShB/EW/DT datasets by expansion, zone and English name. 468 anchor coordinates differ. These are frequently different valid packs, not errors: the distant Defective Turret, Outrunner, Woodbat and Yyenisheyni Bat anchors are supported by the individual maps on FFXIVHunt.com.

Every daily target was then matched to its observation map by expansion and name (important: Rhino Beetle exists in both Stormblood and Dawntrail). All 476 maps were fetched. A raster screen compared each anchor's projected map position to observed spawn markers, with a 4% map-width review threshold. Defective Sentry R8's image has circular areas instead of red spawn dots; its existing anchor was visually checked inside a circle. Walking Tree's anchor was also visually checked in its circled group. No confirmed incompatible daily anchor remained, so no daily point was moved merely to match another valid pack. The 476 reference URLs, comparison anchors and review outcomes are in `data/coordinate-audit.json`.

Sources: https://www.xivdaily.com/hunts/hw (and sb/shb/ew/dt); https://www.ffxivhunt.com/hunt_daily_hw.php (and sb/shb/end/dt). Images were used only for review and are not bundled in the plugin.

## Weekly B-rank targets

The old dataset was a zone-wide union. It could suggest the other B-rank's points: e.g. Amh Araeng displayed 18 possible points for either Worm of the Well or Juggler Hecatomb. The target-specific source has nine points for each, in different parts of the map.

The replacement source is RKI027/ffxiv-huntmaps-maker `047ac3fddf28eb3232c59ed7006d085584963a9d`, `data/marks.json`, MIT. All 77 B-rank names and zones were matched to installed MobHuntOrder/BNpcName/Map/Territory data, including 17 ARR targets and 60 HW-through-DT targets. The resulting 77 sets contain 922 point entries across 47 territories; shared spawn positions can appear in multiple sets. The plugin now selects by target ID and validates map and territory. An unknown/mismatched target displays no unrelated fallback locations.

The target coordinates were also compared to the previous HuntHelper observations: the largest nearest-point discrepancy is under one displayed map unit; the material correction is target assignment, not a claim that observed B-rank coordinates are exact or exhaustive. Source SHA256 and per-target point hashes are recorded for reproducible checks; `tools/verify_coordinate_data.py --huntmaps <pinned marks.json>` checks all assignments without network access.

## Completion status

The game clears the obtained flag after a completed bill but retains its ID and kill counters (documented in FFXIVClientStructs MobHunt). The old status code inferred completion only from still-held entries, causing a finished set to appear uncollected. Completion now checks the retained order requirements, validates the order ID, and requires the completed set to match the currently available set. A green check and daily completion text remain after the bills disappear. New available-set IDs remove the check; empty, invalid or partially discarded bills cannot complete it. No local pickup history is needed.

## Limits

These are offline source/data/UI checks. They do not prove live presence, spawn exhaustiveness, exact roaming positions or server state. Daily anchors are approximate group locations; weekly points are possible spawn locations. No game UI was controlled. Native map markers, real loading and completion status should be checked in game after updating.
