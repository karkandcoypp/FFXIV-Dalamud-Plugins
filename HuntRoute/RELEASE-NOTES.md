# Hunt Route 0.3.0

- Preserve following across zone loading and temporary missing player/ContentId state. Only real logout/character changes stop the session. The next zone's first required target is flagged automatically after arrival, without pressing Start Route again.
- Detect same-territory loading too and retry flags when the map/bill snapshot is not ready.
- Offer an optional cheap shortcut only for a genuinely long intra-zone flight: 1200 m direct, 900 m saved, 300 m maximum remaining leg and 200 gil maximum quoted cost. Ordinary target clicks still never cast an intra-zone teleport.
- Replace ImGui travel-offer windows with native game-style Yes/No dialogs. No/close keeps following and does not repeat the offer. Yes validates target and price again; queued/duplicate/stale answers cannot cause extra teleports.
- Bundle pinned MIT KamiToolKit for owned native UI elements, without intercepting the game's shared SelectYesno window.

Local checks cover arrival continuity, shortcut thresholds, actual update/teleport/map wrappers, deferred native callbacks, queue serialization and compact main-panel layouts. Native dialog rendering and real loading/teleport behavior require an in-game check after updating through Dalamud.
