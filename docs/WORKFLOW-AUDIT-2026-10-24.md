# Workflow stability audit — 2026-10-24

Scope: the eight existing workflows covering BagSense, Eorzea Compass, Eorzea Event Tracker, Ultrawide Cutscenes and both data collectors, plus a new workflow/publication contract check.

## Findings and corrections

- Shared `dalamud-publish` concurrency coalesced pending jobs across unrelated plugins, even with cancel-in-progress disabled. Release groups are now per workflow/ref; each data collector has its own group. Actual index writes use optimistic Git retries rather than relying on a workflow-wide lock.
- All four releases use one compare-and-swap publisher. It reads the latest main tree, merges only pluginmaster.json, commits with that exact parent, and retries a rejected non-fast-forward. It does not reset the checkout or replace unrelated files. Duplicate entries are rejected; rerunning an old tag cannot downgrade a newer entry.
- Published assets are verified before reuse. Existing releases are not blindly overwritten (`--clobber` removed); missing assets can finish publishing after a partial failure. Both a new release and an already published package must pass manifest/version/API/DLL/ZIP checks.
- All four release workflows support manual validation-only dispatch, with the matching tag supplied as input. Their release creation and index mutation steps run only on tag-push events. Validation artifacts contain the actual package, manifest and proposed index.
- Data pushes retry bounded non-fast-forward conflicts after a rebase, preserving newer index changes. Data publication is limited to main. Genuine file conflicts fail instead of silently overwriting newer content.
- BagSense repeated no-change collections previously removed the immediately preceding content-addressed database. PreviousFile now persists across unchanged runs, including migration from a legacy two-payload directory. A pure .NET client-parser/integrity/cache check gates database publication on Linux.
- Build workflows explicitly install Python and .NET 10, have bounded job timeouts and avoid duplicate tag-push builds. Official action versions were refreshed from their published releases; workflow syntax/expressions are checked with actionlint 1.7.12.

## Validation

Eight shared publication tests exercise all four package identities, wrong tags/manifests, unsafe ZIP paths, real isolated Git repositories with a competing push, bounded failure, preserved working indexes/unrelated files, superseded releases and mocked GitHub release creation/reuse. Existing four repository merge tests pass. BagSense has 22 collector/research/retention tests and 28 actual client database integrity/cache checks.

A new CI workflow lints all workflow files and runs publication contracts on main/PR changes to shared tools or workflows. Manual validation runs are used to exercise each release without changing published versions; both data workflows are dispatched against main for live publication checks. External upstream outages can still fail a run; validation gates and retained caches remain in place rather than treating incomplete data as valid.

## Completed GitHub verification

| Workflow | Successful run |
|---|---|
| BagSense build | [40083140457](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083140457) |
| Compass preview | [40083140446](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083140446) |
| BagSense release validation | [40083140422](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083140422) |
| Compass release validation | [40083142233](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083142233) |
| Ultrawide release validation | [40083144105](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083144105) |
| Event Tracker release validation | [40083146101](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083146101) |
| Lodestone live publication | [40083149818](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083149818) |
| BagSense live publication | [40083147880](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083147880) |
| BagSense unchanged repeat | [40083254543](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083254543) |
| Workflow/publication contracts | [40083210966](https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins/actions/runs/40083210966) |

The first new lint run found an unused retry counter under Linux shellcheck; retry attempts are now logged and the subsequent contract run passed. Independent downloads validated all four CI ZIPs and proposed indexes. The public pluginmaster.json stayed unchanged. The second BagSense collection kept the same current and PreviousFile names; the previous gzip was downloaded publicly and its content-addressed SHA-256 and item catalog were verified. No plugin version was released by these validation runs. Git push/retry races and release API mutations were exercised in isolated Git repositories and mocked gh calls, not by overwriting production releases.
