# Hunt Route 0.3.2

- Fix the Endwalker hunt-board button: teleport to Old Sharlayan (aetheryte 182, territory 962, map 693), and mark the board at X:11.8 Y:13.2. Previously it combined Radz-at-Han IDs with Sharlayan coordinates.
- Add a game-data regression check for the intended city, aetheryte, map and board coordinates.

Build, data and route checks are local validation; actual in-game teleport requires verification after updating.
