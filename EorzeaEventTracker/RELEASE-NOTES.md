# Eorzea Event Tracker 0.17.0

- Quest progress now explains missing level, current class/job and previous quest requirements using installed game data for every tracked quest, including future feed events.
- AND/OR prerequisite groups retain their meaning: Gold Saucer requires one city-state envoy quest, not all three.
- Expand How to unlock to follow prerequisite chains (up to four levels) and open validated issuer map locations. Names remain English.
- Already accepted quests display In progress and direct players to the current journal objective instead of the starting NPC.
- Completed quests are green, unfinished quests amber, known access blockers soft red, and unavailable character data gray. Status labels remain visible alongside colors.
- Seasonal and unsupported special conditions are explicitly identified; passing basic checks is not presented as guaranteed NPC availability.
- Requirements refresh for the current character alongside quest progress. Existing profiles and acquisition history are preserved.

Validation: client policy tests, installed-game Gold Saucer/Blunderville prerequisite checks and Release build. Live in-game rendering and map clicks require player verification.
