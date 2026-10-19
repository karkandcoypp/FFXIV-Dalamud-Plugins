# Eorzea Event Tracker 0.21.0

- Added case-insensitive reward search by English name, with multi-word queries, clear action and visible result count. Search combines with remaining/favorites filters and resets on event or character changes.
- Rewards now group missing favorites first, other missing rewards next, ignored missing rewards below active goals, and all obtained rewards last (including manually obtained favorites).
- Choose recommended/feed order, alphabetical order, or ascending prices within each currency and status group. Unknown prices follow known prices. Sort preference is saved per character; collection totals and favorite budgets are unaffected by search.
- Added a prominent deadline panel with larger countdown, colored edge and text explanation: calm green above 72 hours, amber at 72 hours or less, red at 24 hours or less. The event list and widget use the same urgency colors.
- Countdown omits zero days for short durations and explicitly identifies the final minute. Upcoming and ended events have separate states.
- No progress migration. Previous releases remain available for rollback.

Validation: reward sorting/search/profile and deadline-boundary tests, full client checks, collector tests and Release build with warnings as errors. In-game appearance still requires player verification.
