# Eorzea Event Tracker 0.24.0

- Quest headings now show their actual in-game quest marker, selected through Quest.EventIconType rather than guessed from event names. Known locked quests use that marker's unavailable variant.
- Journal and widget frames use thin graphite borders, restrained highlights, rounding and a small native border shadow.
- Journal tabs have equal-height headers, compact journal/compass/gift pictograms and a gold active underline. Narrow layouts retain readable tabs with native scrolling; content controls keep their original compact padding.
- Reward icons have rarity-colored inventory frames and a separate obtained checkmark. Prices include the installed game's currency icon when available.
- Shared styled tooltips across the journal and widget use a larger heading, divider, description and conditions. Reward tooltips include the game's item description when present; map and quest-state hints explain availability.
- Reward cards receive a subtle hover highlight. Clickable reward names underline on hover and retain their verified wiki links.
- Missing assets fall back to text. Character progress, preferences and prior releases are preserved.

Validation: installed-game quest-marker/currency-texture/rarity checks, Release build with warnings as errors and existing client/collector regression checks. In-game rendering and interaction require player verification.
