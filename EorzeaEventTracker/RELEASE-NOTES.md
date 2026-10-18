Eorzea Event Tracker v0.7.0 for Dalamud API 15.

- Added a Not interested checkbox to reward cards. Excluded rewards appear muted with a neutral gray background and remain available to review or restore.
- Excluded rewards count toward neither obtained nor total rewards in the journal, event sidebar and compact widget. They are also omitted by the remaining-rewards filter.
- Marking a favorite as Not interested removes it from favorites and deadline reminders. Favoriting it again restores interest. Main-quest completion remains independent.
- Preferences persist locally by item ID and are shared between characters, like favorites. Actual reward tracking/history is preserved.
- English and Russian labels and explanations are included.

Update through `/xlplugins`, open `/events` and mark Not interested on unwanted reward cards. Clear the checkbox to include a reward again. Turn off remaining/favorites filters to see excluded cards.

Validation: Release build, persistence and exclusion-count checks, reminder exclusion, all-rewards-excluded case and existing feed/collector/localization checks. Muted card appearance should be checked in game.
