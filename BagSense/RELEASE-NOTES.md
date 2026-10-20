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
