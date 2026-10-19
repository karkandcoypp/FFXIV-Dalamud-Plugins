# Eorzea Event Tracker 0.20.0

- Added a Next step block below the selected event heading. It uses the current character's loaded quest progress, access requirements and reward goals.
- Follow the main objective and its prerequisite chain; show class/level blockers and preserve alternative unlock routes instead of choosing an arbitrary city. Already accepted quests point to the current journal objective.
- After quests, show the currency still needed for favorite rewards. Farming links to How to join; merchant maps are recommended only when known currency totals cover the selected rewards.
- Unknown prices, balances or collection states prompt review instead of invented totals. Completed favorites or tracked collections offer to hide the event.
- Future/ended events, incomplete tracking and unsupported conditions have explicit guidance. Prerequisite-only alternatives in automatic feeds are not treated as additional mandatory objectives.
- No configuration migration. Previous releases remain available for rollback.

Validation: client decision-policy and regression checks, collector tests, Release build with warnings as errors. Live in-game rendering and map clicks require player verification.
