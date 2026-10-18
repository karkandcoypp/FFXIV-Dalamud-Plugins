Eorzea Event Tracker v0.5.0 for Dalamud API 15.

- Added deadline reminders when the current character still has unconfirmed favorite rewards: once within 3 days and once within 24 hours of an active event ending.
- Reminders appear as a Dalamud popup and a local chat message, with the event name and missing-favorites count. English and Russian are supported.
- Reminder history persists per character, event and deadline, preventing repeated alerts on scans, relogs or restarts. A late login sends only the most urgent reminder.
- Unknown reward states are excluded. Checks wait for loaded character tracking and the login feed refresh, and run every minute while in game.
- Enabled by default; a separate toggle in Settings disables favorite deadline reminders independently of login announcements.

Update through `/xlplugins`, open `/events`, and mark the rewards you want as Favorite. If the game was closed at a deadline threshold, the current reminder appears after the next login while the event is still active.

Validation: Release build; threshold boundary, persistence, character isolation, late-login, completed/unknown reward, expired-event and opt-out checks; existing feed/collector/localization tests. In-game notification display still needs verification after installation.
