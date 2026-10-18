Eorzea Event Tracker v0.4.0 for Dalamud API 15.

- Favorite individual rewards and filter the list to your favorites. Selections persist across restarts and are shared between characters; completion still follows the current character.
- Open the in-game map with a flag at the quest NPC or reward vendor using Where to go. Current events include quest givers, the All Saints' Wake vendor and the MGF trader.
- NPC destinations and their translations update through the online feed. Missing destinations are not guessed; invalid metadata preserves the last valid feed.
- English and Russian interface labels are included.

Update through `/xlplugins` using your existing custom repository. Use `/events`, open Your progress, mark Favorite on a reward, and enable Favorites only. Where to go opens the map; How to join lists event NPCs.

Validation: Release build, feed/cache/configuration checks and collector/repository/localization tests. Final in-game interaction should be checked after installation.
