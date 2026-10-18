Eorzea Event Tracker v0.10.0 for Dalamud API 15.

- Added an illustrated open-book background with blank parchment pages and a central binding, inspired by a fantasy storybook journal.
- Events are illustrated entries on the left page; the selected event, quest journal and reward cards are on the right page. The binding adapts to the layout instead of crossing the content.
- Introduced dark ink text, paper-colored cards, restrained brown/gold controls and green completion marks. Cover images retain a dark text overlay for readability.
- Page margins protect text from the book edges. Long translations wrap, rewards adapt to one/two columns, and settings scroll in their own panel.
- Favorites, ignored/manual rewards, progress, map actions, reminders, scale and background-opacity settings are preserved. The compact HUD stays compact and separate.
- Version 0.9.0 is preserved in its existing release and backup-eet-before-book-v0.9.0 tag for rollback.

Update through `/xlplugins` and open `/events`.

Validation: Release build, existing feed/progress/localization checks, bundled book PNG and install ZIP checks. Final layout should be checked in game at the player's scale setting.
