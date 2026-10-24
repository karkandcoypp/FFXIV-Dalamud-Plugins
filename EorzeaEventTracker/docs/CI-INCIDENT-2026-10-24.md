# Lodestone / release CI investigation — 2026-10-24

## Repeated collection failures

Runs [40063815401](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40063815401) and [40027438951](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40027438951) fail in `test_current_feed_contains_complete_online_definitions`, before live collection starts. Its hard-coded `lds-img.finalfantasyxiv.com` prefix rejects an `img.finalfantasyxiv.com` banner already supported by enrichment and `FeedPolicy.IsImageUrl`. This is a test/contract mismatch, not evidence of a Lodestone outage.

The fix centralizes Python image URL validation, uses it in enrichment and feed tests, and shares fourteen positive/negative URL fixtures with the actual C# policy. Both official hosts work; non-HTTPS URLs, unrelated or spoofed hosts, user credentials, non-default ports, malformed and overlong URLs remain rejected. Collection now runs the full client policy checks before fetching/publishing.

## Earlier release failure

[39274283674](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/39274283674), tag `eet-v0.25.0`, was blocked by the relationship validator: rewards with omitted `currency` were interpreted differently from the client's default `MGF`, despite `currencyItemId=41629`. The default was aligned in 0.25.1; [39274855374](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/39274855374) succeeded. This investigation adds an explicit regression for the omitted default while retaining rejection of contradictory labels and IDs. The publication gate is not bypassed.

## Verification and release safety

Local verification: 64 Python tests; the full C# FeedChecks suite, including fourteen shared URL fixtures; actual current feed validation with the client policy. Temporary files are redirected into the workspace under restricted local execution. No new plugin binary version is needed for the collector/test/workflow correction.

`eet-collect.yml` can be dispatched against main to exercise real collection, relationship validation, client validation and feed publication. `eet-release.yml` now supports a validation-only manual dispatch with a `tag` matching the checked-out manifest, for example `eet-v0.25.1`. It performs the normal tests, live relationship audit, Dalamud API check, production build and temporary repository-index generation, and uploads the validated package. Release creation and index commits are restricted to tag-push events. This allows checking the release pipeline without replacing an existing release or changing other plugin entries.
