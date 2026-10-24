# CraftProfile 0.1.0

Automatic local profession exports for Dalamud API 15 (.NET 10).
Command: `/craftprofile`. Toggle English/Russian in the window.

## Install
Add the existing custom repository in Dalamud settings:
https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/pluginmaster.json
Then install CraftProfile in /xlplugins. If the package was installed through
this feed it can use the same normal update path as the other plugins.

No external account, API key, HTTP upload or gameplay automation is used.
No process attachment, game command execution, automatic job switching,
crafting, purchasing or materia melding. The collector reads data through
Dalamud services and the bundled FFXIVClientStructs on the framework thread.

## Automatic collection
* All 8 crafting and 3 gathering levels/experience; crafter desynthesis levels.
* Specialist assignments for all crafters. Equipped soul is visible as equipment;
  assignment is different from a soul currently being equipped.
* All saved profession gearsets, HQ/NQ, item IDs/names/item level, 5 meld positions,
  materia type/grade/item ID/stat/value; normal slots and advanced-melding flag.
* Actual equipped gear condition and current craftsmanship/control/CP or
  gathering/perception/GP. Current attributes include all active effects.
* Status IDs/names/parameters/durations. Baseline stats are saved only when no
  statuses and no level sync are present; no guessed subtraction of buffs.
* Master recipe books and gatherer folklore unlocks. Folklore lookup uses
  GatheringSubCategory.Division, not its row ID.
* All crafting recipe unlock checks and requirements (job, level, stat thresholds,
  book, required item/status, specialist/expert flags, ingredients, yield,
  difficulty, quality, durability and simulator divisors/modifiers).
* Craft/gather actions, level and quest gates, conditional specialist requirement.
* Loaded bags, crystals, currency, equipped/armoury items and accessible saddlebag/
  cosmopouch containers, with NQ/HQ quantities and explicit container coverage.

Default export interval: 15 seconds. Capture checks run once per second; recipe
unlock scanning is spread over frames (160 rows per frame), refreshed every five
minutes or using `/craftprofile refresh`. Login and character changes reset the
catalog. Two equal consecutive job/gear/stats/status samples are required before
an equipped observation is accepted. Captured stats are stored per profession.

Equip Goldsmith and Weaver once and wait about 20 seconds on each. The plugin
captures the rest automatically; screenshots and hovering item tooltips are not
needed. Saved gearsets can be exported immediately without job switching, but
are saved definitions, not proof of live stats or ownership.

## Local JSON contract
Default folder:
`%APPDATA%/XIVLauncher/pluginConfigs/CraftProfile/`

* `latest.json`: schema_version, logged_in, character_key, updated_at,
  relative profile_file, error. Logout, plugin unload, pause or capture errors
  invalidate the active session instead of presenting old data as current.
* `profiles/<content-id>/profile.json`: versioned snake_case profile containing
  professions, saved_gearsets, observations, unbuffed_observations, master_books,
  folklore, recipes, abilities, inventory, inventory_coverage and limitations.

Files are replaced atomically. Profiles are isolated by character Content ID;
public release packages never include personal exports. Existing observations
survive restarts; their captured_at and equipment fingerprints remain intact.
A baseline is invalidated when a different equipped set is observed with buffs.

Unknown/missing does not mean zero, locked or safe to craft. Retainer inventories,
retainer sales, unobserved storage, HQ rotation reliability, total synthesis
failure probability, repair/travel costs, crafting speed and future market share
are **not inferred**. No macros are read or executed. Recipe unlocks do not imply
sufficient stats, required equipment/status or reliable HQ. A simulator or verified
rotation remains necessary before spending money.

## Scanner integration
The local Craft Profit Scanner auto-detects latest.json. `--profile-check` checks
it without network calls; `--character-profile PATH` selects an explicit export.
Freshness limit is 5 minutes; per-job stat observations older than 24h are unknown.
Recipe requirements are annotations; passing thresholds never guarantees HQ.
Blocked NQ candidates are excluded from Telegram. Owned materials are displayed
informationally; the scanner does not consume them or silently treat them as free.

## Build/verification
`./CraftProfile/build.ps1` from repository root; `-Offline` disables only the local
NuGet audit lookup when disconnected, not CI checks. The package contains exactly
CraftProfile.dll and CraftProfile.json plus a separate SHA-256 sidecar.
Policy checks run without a game and verify fingerprints, buffs, sync, character
isolation, corruption recovery, JSON shape and atomic writes.

Runtime verification still requires installing in a real FFXIV session and checking
GSM/WVR stats/materia against the game, specialist Alchemist, master books,
crystals, login/logout, job changes and the scanner's local profile check.
A successful build/feed check alone does not verify game memory or UI behavior.
