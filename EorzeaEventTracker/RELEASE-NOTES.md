Eorzea Event Tracker v0.8.0 for Dalamud API 15.

- Redesigned event headers as journal covers: cropped artwork, a soft dark gradient, enlarged wrapped title, status and countdown over the image. Missing/loading artwork uses the same readable cover layout.
- Reworked rewards into compact content-sized cards: icon and title/price side by side, status underneath, then favorite/ignore controls and map action. Long translations and narrow columns adapt without fixed tall empty tiles.
- Introduced a quieter journal palette: warm charcoal panels, parchment-colored text and muted gold highlights. Green is reserved for collection progress and obtained states.
- Updated selected event cards, tab/button states, subtle borders, main title bar and compact widget to match.
- Existing tracking, map links, favorite/ignore behavior, reminders and automatic opening rules are preserved.

Update through `/xlplugins`, open `/events` and use `/eventswidget` for the matching compact display.

Validation: Release build against Dalamud API 15 plus existing localization, feed and progress checks. In-game layout at the player's UI scale needs visual verification after installation.
