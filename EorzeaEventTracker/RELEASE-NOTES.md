Eorzea Event Tracker v0.11.0 for Dalamud API 15.

- Replaced the scrolling book layout with fixed paper leaves. Contents and event sections use numbered pages with previous/next arrows; the mouse wheel turns pages instead of scrolling.
- Added paper bookmarks for Overview, Participation, Quests and Rewards. The overview cover appears on its own first leaf, with event details on later leaves.
- Text wraps and continues on additional pages. Rewards are packed into one/two-column leaves using measured card heights and the available page area. Tight layouts use an Actions popup, preserving favorites, ignored/manual marks and map actions.
- Added a short page-turn shadow. Settings use five separate leaves rather than pushing the book content downward.
- Window bounds follow the game viewport. Requested font scale is capped when needed to keep the book on screen; page density adapts to the actual font size.
- Preserved version 0.10.0 with backup-eet-before-pagination-v0.10.0 and local source/install ZIP copies for rollback. Previous backups are retained.

Update through `/xlplugins`, open `/events`, select an event, use the bookmarks and turn leaves with the arrows or mouse wheel. A small reward card's Actions button opens all reward controls.

Validation: Release build, pagination coverage and height limits at 75/100/125/175%, narrow text wrapping, Unicode graphemes, existing progress/feed/localization checks and install ZIP checks. Final page layout at the player's UI scale still needs in-game verification.
