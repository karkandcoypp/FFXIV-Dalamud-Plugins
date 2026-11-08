# 0.3.7

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
