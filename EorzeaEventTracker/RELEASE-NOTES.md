# Eorzea Event Tracker 0.16.0

- Quest titles now always remain English, alongside item and NPC names. Event descriptions and interface text keep the selected language.
- Hidden events, favorites, ignored rewards, filters, language, scale, opacity, notifications, login opening and widget preferences are now stored per character ContentId. Window layouts also have separate character identities.
- Character switches reset temporary journal state and load the correct profile before automatic visibility and reminder decisions.
- Existing character acquisition histories and manual obtained marks are preserved. Old global hidden/favorite/ignored selections remain archived in configuration, but are not assigned to an unknown character; each new profile starts with these selections empty.
- The event feed URL remains a shared data source. Existing display preferences provide the initial defaults for new character profiles, after which edits are independent.
