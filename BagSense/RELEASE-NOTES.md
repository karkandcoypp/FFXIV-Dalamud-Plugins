# BagSense 0.3.0 — quest protection and an updating item database

- Complete searchable item catalog: 50,880 named items in the initial data set.
- Daily GitHub collection from a pinned ffxiv-datamining revision; the plugin checks for updates on load and daily, with a manual refresh button.
- SHA-256, schema, count and reference validation before applying a catalog. Verified disk cache and installed-game fallback keep the plugin usable offline.
- Quest objective text, objective number, known quantity/quality, evidence and the specific hand-in NPC/map when resolved.
- Active quests automatically reserve required stock and constrain existing cleanup plans. Independent personal goals add to these reservations.
- Unknown quantities, unknown quality, unreadable journals and HQ needs keep all potentially needed copies. Inactive quests create no automatic reserve.
- Inventory disappearance does not prove a hand-in. Reservations stay until the quest completes.

Initial quest coverage: 1,020 item links across 629 quests, 245 identified hand-in objectives, 118 exact quantities. Other scripts and intermediate variable flags remain partial. ToDoQty is not an item count. My First Pickaxe correctly keeps ten Copper Ore for Adalberta rather than interpreting its one NPC interaction as one ore.

Обновись через `/xlplugins`, открой `/bagsense`. Новая вкладка «База предметов» показывает полный каталог и дату обновления. Квестовые карточки объясняют, сколько оставить и куда сдавать. При неизвестном количестве активного квеста весь запас сохраняется.

Protection applies to BagSense's cleanup plan; the plugin does not intercept manual in-game sales. All actions remain manual. Character data stays local. Quest/item names and original journal text use English; UI supports RU/EN. In-game quest transitions and map clicks still need live testing.
