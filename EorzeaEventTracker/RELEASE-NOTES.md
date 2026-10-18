Eorzea Event Tracker v0.6.0 for Dalamud API 15.

- Added a separate compact widget with active events, time remaining, confirmed reward counts and progress bars. Soonest-ending events appear first.
- Toggle it with `/eventswidget`, `/events widget`, or Show compact event widget in Settings. The visibility preference survives restarts; the widget hides while logged out.
- Click an event title to open that event in the main journal. Long translated titles wrap, and the final 24 hours are highlighted.
- The widget can be moved and resized. While character data is loading it shows a waiting status; events without tracking have an explicit status.
- English and Russian labels are included. The main journal and deadline reminders continue working independently.

Update through `/xlplugins` and run `/eventswidget` to show it. Close its window or use the same command to hide it.

Validation: Release build and existing feed/collector/localization checks. Layout and clicks should be checked at the player's UI scale in game.
