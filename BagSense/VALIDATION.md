# BagSense 0.4.0 validation

21 October 2026, Windows, .NET 10, installed Dalamud API 15.

## Evidence

- Release build treats warnings as errors; package manifest and assembly version agree.
- 111 policy checks include accepted-job requirements, unknown collection state, mount/minion/music classification, reservations, old plans and character isolation.
- 13 collector checks cover namespace separation, explicit/negated dialogue requests, HQ inside item phrases, literal quantities and ambiguity handling.
- 22 catalog/update checks validate all-quest coverage, structured job alternatives, key items, map targets, SHA-256, counts/schema, cache persistence, offline and timeout fallback.
- Existing 11 configuration round trips and 29 installed-game checks include actual mount action/collection identity, journal artwork ID and an exchangeable mount from Aloalo Coin.
- Production UI is rendered in RU/EN with actual extracted game icons and mount artwork. Fixtures cover inventory/goals/plan, item database, all-quest view and a mount exchange at default/minimum sizes. Quantities, collection flags and journal state are sample values.

## Database

Pinned ffxiv-datamining revision: 44948e4cda5846eab0baa9872122a888fcc4f1b1.

- 5,193 named Quest rows with nonzero journal genre, all corresponding text sheets collected.
- 54,411 named items: 50,880 ordinary Item rows and 3,531 EventItem rows.
- 4,177 item/quest associations; 687 known quantities, including 517 ordinary-item quantities.
- 1,206 identified hand-in objectives; 1,528 associations have NPC locations, including structured supply NPCs whose exact objective is unresolved.
- Exact supply counts come from AmountRequired and ItemHQ. Job membership is retained from ClassJobCategory/ClassJob.
- Fallback text counts retain their literal evidence and require an unambiguous hand-in objective; dialogue fallback additionally requires explicit request language.
- Map 0 / territory 0 placements are rejected. Ambiguous global NPC positions are not substituted for objective locations.

## Mount imagery

Chocobo Whistle action 1322 resolves to mount 1; Mount.Icon 4001 maps to journal artwork 68001. The installed ui/icon/068000/068001.tex and 068288.tex decode to 192x192 images, distinct from a 40x40 mount-list icon. Runtime uses Dalamud's managed texture provider. Standalone UI renders consume the same extracted assets with preserved aspect ratio; they do not prove host texture-loading behavior.

## Boundaries and live checks

All named journal quests are enumerated; unsupported server-side conditions, intermediate variables, Leve records and other separate delivery systems are not claimed as fully solved. Unknown/HQ needs stay protected. Protection constrains BagSense's cleanup plan, not manual in-game selling.

In FFXIV: update via /xlplugins; confirm a mount reward preview/type/ownership and a profession quest's current branch; test map clicks, journal completion/abandonment, character changes and network loss/restart. Standalone input substitutes do not validate typing or native interactions.
