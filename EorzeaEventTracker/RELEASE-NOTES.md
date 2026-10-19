# Eorzea Event Tracker 0.22.0

- Obtained rewards now live in a separate Obtained — N section, collapsed by default. Expansion state is saved per character and event occurrence. Search and filters also apply to this section; collection totals and favorite budgets are unchanged.
- Added reward-type filters: gear, mounts, minions, housing and other. Types come from installed item/equipment/action data, so new feed rewards are classified automatically. Housing includes exterior furniture and fixtures; unclassified items remain under Other.
- New badges identify event occurrences not yet viewed on the current character. They clear after two seconds of continuous visible detail viewing. Ordinary feed/translation refreshes do not re-mark known occurrences; a later recurrence is new.
- Added a clearly labeled feed publication timestamp beside event details, distinct from local refresh/download time. New markers also appear in the compact widget.
- Type filters, obtained-section preferences and viewed-event history are character-specific. Existing progress and preferences are preserved.

Validation: category/filter/discovery/profile regression tests, installed-game category/action checks, full client/collector checks and Release build with warnings as errors. In-game rendering still requires player verification.
