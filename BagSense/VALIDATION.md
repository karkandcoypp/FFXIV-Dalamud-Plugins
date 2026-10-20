# BagSense 0.6.0 validation

21 October 2026, Windows, .NET 10, installed Dalamud API 15.

- Full pass over every one of 54,411 catalog items; complete local per-item report in artifacts/bagsense-audit-v6.json, committed scope/remaining IDs in coverage-audit.json. Exact-linked uses (15,639), category explanations (36,195) and unresolved use (2,577) are not conflated. 37,474 records have an indexed acquisition path.
- 135 policy checks include pre-acceptance demiatma protection, completed one-time release, level/job/prerequisite explanations, OR alternatives, unknown state, accepted quests and merchant-source sale safeguards.
- 14 Python collector checks; published catalog decoding additionally checks demiatma quantity=3, objective=2, mapped Gerolt and quest-browser relationships. Existing cache, hash, path and timeout checks remain required.
- 37 installed-data and 11 persistence checks; complete catalog audit asserts Arcane Artistry level 100, Unfamiliar Territory, Gerolt and Urqopacha source. Production catalog construction is exercised, not mocked data-index code.
- 47 production UI scenarios in RU/EN, with actual game icons. Added Azurite Demiatma at default/minimum size, missing prerequisite and low current-job level. Fixtures are sample character states, not captured live values.
- Live FFXIV map clicks, native completion/job queries and quest transitions remain unverified. Additional server conditions are explicitly unverified in the interface. No claim of perfect item-use or quest-script coverage.

Database: ffxiv-datamining 44948e4cda5846eab0baa9872122a888fcc4f1b1; Teamcraft f2918cca2b159d05f569e24b5828b5af38444d0a. 4,800 item/quest links, 693 confirmed quantities, 20,630 additional acquisition links. Collector pins current upstream commits on each daily run and rejects significant source shrinkage.

---

# BagSense 0.5.0 validation

21 October 2026, Windows, .NET 10, installed Dalamud API 15.

- 125 policy checks: accepted-leve reservations, repeats, multiple independent quests, unreadable journal fallback, learned-duplicate safeguards and actual quality eligibility, plus existing persistence/routing behavior.
- 37 installed-data checks and 11 configuration checks: all 1,240 named CraftLeve entries have positive quantities and mapped recipients; leve 36 maps to Audrie; Zhloe delivery and collectable appraiser routes resolve; 1,610 collectable rows and 3,229 unlock items are indexed.
- Production RU/EN UI fixtures include accepted leve hand-in, custom delivery, owned mount and minimum window size, alongside existing inventory/goals/plan/database/quest views. Character flags and quantities are fixtures, not captured live player state.
- Existing collector, database integrity/cache/update and package/feed preservation checks remain required by the build/release workflow.
- Live native journal transitions, current-character unlock queries, current weekly request selection and map clicks still require FFXIV testing. There is no in-game validation claim.

## Sources and interpretation

Installed Lumina sheets: CraftLeve, Leve, Level, LeveRewardItem/Group, SatisfactionNpc, SatisfactionSupply/Reward, CollectablesShopItem/Refine/RewardScrip. CraftLeve uses Leve.LevelLevemete for delivery NPC placement (also the delivery location in Teamcraft's leves extractor); its table name must not be interpreted as evidence that the NPC is the issuer. Actual leve 36 recipient Audrie verified against installed NPC data. LeveWork exposes LeveId/Sequence; normal and leve journal IDs remain separate. HQ bonus is not treated as mandatory HQ. Random reward group chance is not a per-item guarantee.

- https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/staging/apps/data-extraction/src/extractors/leves.extractor.ts
- https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Application/Network/WorkDefinitions/LeveWork.cs

---

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
