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
