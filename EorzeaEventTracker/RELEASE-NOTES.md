# Eorzea Event Tracker 0.24.1

- Fixed quest icons showing diagnostic text such as MAP ANONC QUEST. EventIconType values identify a texture family; its base image is a debug label, while offset 1 is the real quest marker.
- Use the larger NPC marker artwork for ordinary and unlock quests, including the proper unavailable variant. Unknown/incomplete families fall back to text instead of displaying diagnostic images.
- Verified actual installed textures visually for ordinary, locked and unlock families, including We Scare Bears and Grim and Grisly Games. Added regression checks using those quests and the shared runtime marker selector.
- Other interface changes and character data are preserved. Previous releases remain available for rollback.

Validation: visual inspection of installed quest-icon variants, installed-game checks, client/collector regressions and Release build with warnings as errors. Live in-game verification remains with the player.
