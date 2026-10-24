# Eorzea Event Tracker 0.25.2

- Updated the bundled Lodestone feed with all three currently tracked events and 31 identified rewards, retaining explicit partial-data status where relationships are incomplete.
- Fixed recurring event-data workflow failures caused by a test that rejected the official img.finalfantasyxiv.com image host. Collector and client now share positive and negative URL contract fixtures; both official image hosts remain supported.
- Added a regression for legacy rewards using the default MGF currency and retained rejection of contradictory currency names, IDs and prices.
- Added a validation-only manual release workflow and verified the production build, relationship audit, package and repository index before publication.

Обновление доступно через Dalamud. Прогресс персонажей и настройки сохраняются. Проверки данных и сборки не заменяют проверку интерфейса и NPC непосредственно в FFXIV.

---

# Eorzea Event Tracker 0.25.1

- Event details now show a reliability badge: reviewed and recently validated, partial automatic data, cached data, or stale data. Hover for the meaning, relationship-check time and outstanding issues.
- Settings → Diagnostics and feed updates shows the installed version, successful feed download time (persisted across restarts), latest attempt, data source and actual error. Copy diagnostics excludes character/progress information and strips URL credentials/query parameters and local file paths.
- Automatic feed publication now audits quest → issuing NPC → map/coordinates and reward → quest or merchant → shop offer/currency/price. Entrance routes are checked separately from the actual destination inside an instance. Definite contradictions stop publication; unavailable, ambiguous and unsupported evidence remains partial. Audit reports are available in workflow artifacts.
- Added regression coverage for mismatched prices, wrong icons and destinations, stale evidence, outages, safe diagnostics, failed downloads and cache receipt integrity. Previous releases remain available for rollback.

The audit verifies supported game-data relationships, not live NPC spawning, event phasing or every conditional shop type. Unsupported relationships are explicitly marked incomplete. In-game rendering and NPC availability still require player verification.
