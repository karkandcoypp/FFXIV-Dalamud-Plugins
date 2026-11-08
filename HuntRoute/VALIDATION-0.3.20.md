# Hunt Route 0.3.20 — ordinary world labels

## Behavior

Independent **ordinary target highlighting** setting, enabled by default, range 150 m (50–500 m). Existing elite setting/range are preserved. Labels contain name, actual bill count and 3D distance. The followed instance gets a brighter color, thicker ring and textual current-target prefix. Without a selected instance, only the nearest candidate for the current requirement is emphasized.

Selection uses value snapshots from client-loaded battle NPCs and current bills. Dead, completed, unread, manually deferred, wrong-zone/wrong-map, invalid-position and out-of-range candidates are excluded. Duplicate species across bills keep independent counts and produce one label per loaded instance. Completing one bill does not hide another unfinished requirement. Loading/cutscenes/invalid player state hide the overlay.

This is read-only presentation: no counter increments, flag writes, map opening, transport, navigation pause changes or persistent NPC IDs. It can operate with the main window closed or following paused, just like the elite overlay. It does not stop the hunt when disabled.

## Verification

- .NET 10 / API 15 Release build, warnings as errors: PASS.
- Route/state tests with real game sheets, seed 1729: 43,360 assertions, including 22 new selector/settings/persistence assertions.
- Map/data/portrait tests: 5,896 assertions, including 90 substituted-controller scenarios. Twelve added runtime assertions cover count completion, exact followed instance, range/toggle independence, deferrals, unread snapshots, map transition, loading/cutscene and no writes/teleports/counter changes.
- 38 main/mini UI compositions, four native-dialog previews and one world-label composition: PASS. The separate range fields are rendered in an expanded settings preview. Current/ordinary/elite colors/text and invalid projection rejection are checked; preview inspected locally.
- Local ZIP, DLL and manifest verifier: PASS. Public package/feed verification follows successful release workflow.

## Runtime boundary

No game control used. Offline projection/drawing and substituted object tables do not prove actual streaming, world-to-screen timing or crowded-scene label readability. In-game check: approach required ordinary mobs, verify name/count/distance and current-instance emphasis, finish the counter and confirm disappearance; toggle ordinary highlighting off while keeping elite on, and verify separate range/persistence after restart.
