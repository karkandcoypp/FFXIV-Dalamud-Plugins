Eorzea Event Tracker v0.9.0 for Dalamud API 15.

- Added Already obtained (manual) to reward cards for items acquired before installing the plugin or otherwise not detected. Manual marks count in all progress views and suppress missing-favorite reminders.
- Marks persist separately per character and never become automatic observation evidence. Clearing a mark restores the automatic state; game-confirmed ownership/unlocks remain confirmed.
- Added Interface scale (75–175%), Background opacity (40–100%), and Reset appearance to Settings. Changes affect the journal and widget while text and icons stay opaque.
- Nested event/reward panels use a consistent local font scale, cover titles retain their relative emphasis, and minimum window sizes follow the chosen scale.
- Appearance settings and manual marks persist across restarts. English and Russian labels are included.

Update through `/xlplugins`. In `/events`, open Your progress to mark a reward, or Settings to adjust appearance. Manual marking is available after this character's tracking data has loaded.

Validation: Release build; per-character persistence, reversible manual confirmation, preservation of automatic evidence, reminder suppression, appearance bounds/defaults and existing regression checks. In-game layout and sliders require verification at the player's UI scale.
