# Hunt Route 0.3.5

- Persist completion in completion-history.json, keyed by the character ContentId and bill tier. Each expansion and character keeps independent daily/weekly progress across plugin/game restarts.
- Expire daily completion at 15:00 UTC and weekly completion Tuesday at 08:00 UTC, including while the client still retains old kill counters. Stale completed sets cannot re-award expired checks; observed fresh pickup permits legitimate repeated order IDs.
- Save atomically on changes, retry failed writes and attempt a final save on logout/unload. Invalid history is logged without breaking hunt routing.
- Refresh the compact UI with slate/lavender colors, mint completion checks, segmented daily/weekly progress, completed expansion labels and responsive expansion selection for narrow windows/large fonts.
- Retain route, map, aetheryte and teleport algorithms. Expand tests for real file reloads, independent characters/expansions, exact UTC boundaries, stale snapshots, failed saves and compact UI layouts.

Offline persistence/data/UI checks and release package verification do not replace live-game validation of restart and teleport behavior.
