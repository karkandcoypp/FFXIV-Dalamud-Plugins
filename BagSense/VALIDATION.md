# BagSense 0.3.0 validation

21 October 2026, Windows, .NET 10, installed Dalamud API 15.

## Verified locally

- Warning-as-error Release build and package manifest/assembly agreement.
- 103 policy checks including active/inactive quests, additive goal reservations, existing sale plans, unknown journals/quantities, HQ protection and reward/input separation.
- 9 collector tests: literal item names, articles, compound/hundred quantities, contradictory amounts/quality and rejection of unrelated counters/materials.
- 17 production catalog decoding/update checks: full item count, Copper Ore's actual quantity/objective/NPC/evidence, unknown/HQ requirements, corrupt checksum, unsupported schema, path traversal count mismatches, atomic cache persistence, network loss, timeout and corrupt-update fallback.
- Existing 11 configuration round trips and 25 installed-game checks retain compatibility.
- Production UI rendering: 31 RU/EN scenarios, including the full item catalog and active Copper Ore quest card. Quantities and quest state are fixtures; text inputs are inert standalone substitutes. This does not test typing or native map clicks.

## Public data snapshot

Source: xivapi/ffxiv-datamining revision 44948e4cda5846eab0baa9872122a888fcc4f1b1.

- 50,880 named items; 1,020 quest-item links across 629 quests.
- 245 hand-in objectives, 244 with an objective NPC map target; 118 unambiguous counts.
- My First Pickaxe: Copper Ore 5106, ten items, objective 1, final sequence 255, Adalberta in Ul'dah. ToDoQty=1 describes the objective, not the required ore count.
- Unknown counts do not become zero or one. Source journal evidence is retained for every inferred exact count.
- RITEM parameters identify ordinary requested items; event-item ITEM constants and unrelated numeric parameters remain excluded.

## Update mechanism

The daily bagsense-data workflow collects pinned CSVs and journals, rejects incomplete/shrinking item sets, and publishes a content-addressed gzip plus manifest. The client uses a fixed repository endpoint, validates bounded gzip/JSON/checksums/references, writes payload before the manifest atomically, and retains its last valid cache on failure. No character state is uploaded.

## Required live checks

1. Update in /xlplugins; open /bagsense and the item database; verify its item count/date and refresh status.
2. Accept a supported hand-in quest and confirm the reserve, objective, NPC marker and available surplus against the in-game journal.
3. Confirm HQ/unknown requirements keep all copies; check an old sale plan is constrained immediately after the quest is accepted.
4. Complete/abandon a quest; confirm its automatic reserve is removed while personal reserves remain.
5. Test RU/EN, character changes, network loss/restart and native map clicks. The South Horn map may require content access.

No claim is made that every quest script, intermediate variable or manual sale is intercepted. Protection constrains BagSense's plan, not the game's selling UI.
