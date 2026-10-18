Eorzea Event Tracker v0.6.1 for Dalamud API 15.

- Automatic journal opening now requires an active event whose main quest is not completed on the current character. No active events, or all main quests complete, means no automatic opening.
- The saved compact widget follows the same automatic display rule. Manual `/events` and `/eventswidget` access remains available, including completed events.
- Login presentation waits for the feed refresh and character progress before deciding. The journal is closed on logout to prevent a previous session's open window bypassing the next login check.
- Main objectives are Grim and Grisly Games for All Saints' Wake and Just Crowning Around for Fall Guys, as selected by the user. Other shop rewards do not affect completion.
- Added Main objective completed status. Main quest IDs for future events can be published in the online feed.

Update through `/xlplugins`. The Open on login setting still controls automatic opening; this update adds completion and event-activity conditions.

Validation: Release build; empty/expired/upcoming feeds, completed and mixed event states, unloaded/new character data and invalid completion references; existing collector/localization checks. In-game login behavior requires verification.
