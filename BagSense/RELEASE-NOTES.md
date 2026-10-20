# BagSense 0.6.0 — item knowledge, quest access and catalog audit

- Audited every one of 54,411 catalog records. 15,639 have linked use relationships, 36,195 have a category/description-based role, and 2,577 still need detailed research. The latter are explicitly unresolved, not declared safe to discard. Full summary and remaining IDs are in coverage-audit.json.
- Acquisition links now include crafting (with ingredient counts), gathering, fishing/spearfishing, reverse special-shop trades, quest rewards and 20,630 Teamcraft enemy/duty/FATE/container relationships. Possible drops are not promised guaranteed rewards.
- All recipe uses and merchant listings are retained; former silent caps are removed. Recipe results open in the item database; source lists can expand to all entries.
- Food, medicine, materia, repair/glamour items, bait, housing, gardening, currency, event items, outfits and legacy records receive role-specific RU/EN explanations. Game descriptions and wiki lookup are visible near the top.
- Wiki links on items, quests and prerequisite quests. Missing wiki titles fall back to wiki search. Quest access explains current level/job and AND/OR prerequisites; special reputation, GC, housing, duty, time and script conditions remain explicitly unverified when player state is not read.
- COST_ITEM input families are indexed. Azurite and the other five demiatma link to Arcane Artistry, 3 each, the exact Gerolt hand-in objective and map. The shared one-time unlock is reserved even before accepting the quest, until completion is known.
- Fixed NPC placement on shared maps/instance variants (including Phantom Village) using the map's canonical territory instead of rejecting a valid Level placement.
- Buying an item again is no longer treated as automatic justification to sell it. Protections and personal reserves remain authoritative.
- Daily collector updates pin both ffxiv-datamining and Teamcraft revisions. Provenance, hash validation, offline cache, goals and plans remain compatible.

Обновись через `/xlplugins`, открой `/bagsense`. У Azurite Demiatma теперь показаны Arcane Artistry, 3 шт., Gerolt на карте, условия доступа и вики. Подробная связь применения пока не подтверждена для 2,577 специальных записей; это ограничение видно в карточке, а не скрыто советом продать.

Local verification does not replace live FFXIV quest transitions, map clicks or native player-state testing. Known prerequisite checks do not claim all server-side acceptance conditions are solved.

---

# BagSense 0.5.0 — deliveries and collection decisions

- 1,240 named tradecraft/fishing CraftLeve records from installed game data: exact quantity per delivery, optional repeat count, level/job and actual hand-in NPC with map button.
- Accepted Leve journal entries reserve the entire possible delivery cycle. Separate leve IDs cannot collide with normal quests. An unavailable journal preserves possible needed stock.
- Custom Deliveries: possible weekly items, recipient maps, minimum collectability and base gil/scrip reward ranges. Weekly selection, allowances and level-dependent currency variants are explicitly left to the NPC window.
- 1,610 collectable shop rows: minimum quality, three quality/reward tiers, level/unlock quest and appraiser maps. Actual stack collectability is read; low-quality copies do not count as deliverable stock.
- Leve base EXP/gil and optional item reward tables are distinguished. Reward cards explain item type, description, icon and collection status; group probability is not presented as a guaranteed item reward.
- Collection scan covers 3,229 unlock items beyond carried items and selected exchange rewards. Owned/missing/unknown are distinct; unknown is never treated as owned.
- A prominent RU/EN item decision explains keep/use/review and removable learned duplicates. Protection, reserves and other indexed uses take priority. Active deliveries appear before unrelated quest details.
- Existing goals, notes, plans and the verified auto-updating quest/item catalog remain compatible. Delivery tables refresh from the installed game after a game/plugin restart; they do not depend on a second external API.

Обновись через `/xlplugins`, затем открой `/bagsense`. Выбери предмет: блок «Сдачи и Leve» показывает количество, качество, награду и NPC с кнопкой карты. У изученных предметов явно виден статус коллекции и решение по дубликату.

Grand Company daily requests and other dynamic server-selected delivery systems are not claimed as live tracked requests. Custom Delivery weekly requests and allowances must be confirmed in game. Local checks do not replace live journal, map-button and ownership testing.

---

# BagSense 0.4.0 — all journal quests and understandable exchange rewards

- A searchable Quest tab covering all 5,193 named journal quests in the current source, with objectives, relevant items, profession alternatives and mapped NPC destinations when available.
- Expanded data: 50,880 ordinary items plus 3,531 key items in a separate ID namespace; 4,177 quest-item links and 687 known quantities (up from 118).
- Explicit QuestClassJobSupply quantities, HQ requirements and accepting-job branches. Clear literal objective/journal/dialogue requests add further evidence. ToDo counters and unrelated script constants are not interpreted as item quantities.
- Active quest reservations select the accepted profession's requirement. Unknown or HQ requirements remain protected conservatively.
- Exchange rewards now have an icon, a plain-language type (mount, minion, music, equipment, etc.), a usage explanation, game description and known collection ownership.
- Mount rewards show the actual 192px journal artwork loaded from the installed game, plus mount name and the whistle/key item that unlocks it.
- Reward filters, explicit required/owned/missing costs and collection types in cleanup-plan destinations.
- Daily catalog updates, verified cache and existing goals/plans remain compatible.

Обновись через `/xlplugins`, открой `/bagsense`. Во вкладке «Квесты» доступны все квесты текущего каталога. В обменах видно, что именно получишь и как использовать награду. Изображения маунтов берутся из игры.

Complete quest enumeration does not mean every server script has a recoverable exact quantity. Unresolved counts/positions remain explicit, and possible needed stock remains protected in BagSense's plan. Leve and other separate delivery systems are not claimed as ordinary Quest records. Quest text uses original English data; interface and explanations support RU/EN. Live map clicks, quest transitions and actual Dalamud texture loading still require in-game testing.
