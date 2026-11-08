# 0.3.17

- Entry selection and teleport now share the reviewed zone plan. Estimates include the current-player leg and known elite spawn circuit, excluding deferred/unavailable anchors. Unknown elite search duration is never invented.
- Destructive bill changes require two consistent observations at least 750 ms apart. Ambiguous/invalid reads retain accepted counters, contract generation and current goal, and block transport/navigation for that bill.
- Shortcut dialogs and notices include order and generation; replacement contracts cannot accept stale proposals. Reset offers acknowledge only categories still available at actual show time.
- Elite observation remains active during navigation pauses without changing flags, visiting points or consuming death grace. Independent user/flag pauses remain in place.
- Independent versioned settings file with atomic saves, backup and migration from the old route file. Separate elite/next-zone sounds; persistent save-error warnings and explicit retry preserve game credit.
- Distinct no-bill, completed, deferred, unavailable, filtered and loading states with matching actions.
- Added pure and substituted-controller regressions plus six UI states. No live-game control or NPC streaming/native teleport/UI verification was performed.

# 0.3.16

- The 100 ms elite branch now suspends during unconscious/loading/cutscene states. Contract credit clears elite tracking before busy-state deferral. Added controller regressions for both paths.
- Includes the full 0.3.14 journey implementation and the explicit board-city confirmation from 0.3.15.

# 0.3.15

- Board confirmations now explicitly show the actual city and aetheryte, followed by the expansion and current price. Added a controller assertion for the displayed destination.
- Includes the full unified journey, lifecycle, recovery, dashboard, mini panel and reset-notice changes from 0.3.14 below.

# 0.3.14

- Unified active-bill route across five expansions, dynamic bill additions, route-length zone ordering and explicit target/zone deferrals.
- Instance-aware elite tracking: observed death waits for fresh credit before resuming, missing visibility retains the anchor, repeated rounds and late detections remain automatic. Nearby ordinary targets use actual positions and stable instance selection. Explicit manual goals win over automatic discoveries.
- Independent pause reasons, external-map-flag ownership, expected/cancelled travel, exact destination/price revalidation and automatic following after arrival.
- Per-character route restoration on pause with atomic state, backup and corrupt-file quarantine. Completion history remains independent.
- Overview-first dashboard, passive name selection, explicit map opening, optional mini panel, settings and local diagnostic export. Reset offers are period/character scoped and do not interrupt an active route.
- Fixed portrait lookup: the prior positional boolean requested item HQ rather than switching high resolution. Explicit high/normal resolution fallback is now tested and texture failures are isolated.
- Extended pure lifecycle, substituted-controller and offline layout checks. No live-game streaming/native-map/teleport/sound validation was performed.

# 0.3.13

- Weekly search automatically starts another round after the last spawn. A new round avoids immediately returning to the last visited point when others exist. Near/single-point repeats require leaving and re-entering the visit radius, avoiding flag spam while stationary.
- The round number is visible. Late detection always wins, including at a round boundary, and stays locked until contract credit.
- Mob-name hover previews are present for both daily and weekly targets, using game hunt portrait icons with high/standard-resolution fallback. Added an explicit missing-image message.
- Local/CI checks do not replace in-game validation of repeated map routes and texture rendering.

# 0.3.12

- Fly-by weekly search: the next point is selected immediately within 45 horizontal yalms if no matching elite is detected. Works in flight without landing, stopping, hovering, timers or manual confirmation.
- Route detection runs at 10 Hz using the same loaded-object detector as the world overlay; contract refreshes retain the existing 1-second cadence.
- Detection takes priority on every scan. Found elites remain locked through loss of visibility and loading until contract credit; combat/casting pause speculative point changes.
- Detection is limited to client-loaded objects. No automatic movement or teleport was added. Tests cover fly-by visits, late detection, lock retention and route continuation; actual streaming needs in-game verification.

# 0.3.11

- Restored automatic elite spawn advancement with a streaming guard: 5 seconds of continuous stable nearby observations, at least 4 samples, maximum 1.5-second scan gap. Fast approach (including vertical movement), flight, loading, combat/casting and leaving the radius reset inspection.
- Each new point is inspected separately; no chain-skipping on a single snapshot. Detection always wins, including on the last inspection tick.
- Found elites remain locked at their latest observed position until contract completion, through temporary disappearance and loading. Manual empty-point confirmation removed.
- Deterministic and randomized tests cover delayed detection, pauses, movement, sparse/duplicate timestamps, loading, lock retention and completion/next-zone continuation. Native streaming still needs in-game verification.

# 0.3.10

- Empty ObjectTable snapshots never auto-skip a weekly spawn. Confirm an empty spot explicitly after arriving and landing; loading, flight, combat and casting disable confirmation. Detection remains automatic, with no dwell timer.
- A detected elite locks the flag to its latest observed position until contract completion. Despawn, lost visibility, death before kill credit, and same-route loading preserve the lock. Re-selecting the tracked elite cannot reset it.
- Expanded deterministic and randomized search regressions cover delayed streaming, flight, combat, marker retries, duplicate points, exhausted searches and completion/next-zone continuation.
- Runtime NPC streaming and native map behavior still require in-game validation.

# 0.3.9

- Removed the elite spawn dwell timer. At arrival within 45 yalms, a single shared detector checks loaded living contract elites; detected targets take priority, otherwise the next candidate is flagged immediately. Combat/casting pause speculative advancement; flight no longer forces waiting.
- World overlays and route search now use the same detector. Exhaustion never completes the contract and detection continues afterward.
- Regression coverage: elite completion (including a disappearing completed bill), immediate next-zone notice, loading transition, automatic first marker and continued following.
- Local and CI verification do not replace in-game NPC streaming/teleport validation.

# 0.3.8

- The Map button previews elite spawns without teleporting or restarting tracking.
- Weekly B-rank targets join the numbered zone route after daily targets. An unfinished elite blocks the next-zone suggestion, including when its spawn data is missing.
- Tracking an elite opens the in-game map and flags the nearest known spawn; no plugin map is opened. Each visited point waits 8 seconds within 45 yalms before advancing. Combat, casting and flight pause the absence timer.
- A matching live, locally loaded NPC overrides the speculative point. Exhausting points does not mark the hunt complete; manual next point/restart remain available.
- Includes redesigned native teleport confirmations from 0.3.6.
- Automated search/route regression checks pass; actual NPC streaming and native UI require in-game verification.

# Hunt Route 0.3.6

- Redesign both native travel confirmation windows with a dark inset panel, lavender heading, destination, separate target card and clearer action labels: Teleport / Later (Russian UI).
- Intra-zone offers show the actual quoted gil cost, the remaining distance after teleport, and three separate estimate cards: direct flight, total with teleport, and time saved.
- Next-zone offers clearly show the completed zone, destination and first target, with automatic marker/following reminders.
- Size text blocks using native game text measurement, wrap long names and reserve button space. Keep native buttons/gamepad navigation with Later focused by default.
- Preserve confirmation serialization, busy/stale/price checks, deferred callbacks and one-action behavior. Add shared-layout, controller and offline composition regressions.

Offline previews verify composition, not FFXIV's actual Atk fonts, theme textures or live interactions. Actual native appearance requires an in-game check after updating.
