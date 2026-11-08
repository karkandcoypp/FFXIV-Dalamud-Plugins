# Hunt Route 0.3.17 — closure of the plan audit

This release addresses the concrete differences recorded in the 0.3.16 plan audit. It does not claim live-game verification.

| Audit difference | Implementation | Regression coverage |
| --- | --- | --- |
| Calculated entry could differ from actual teleport; incomplete available-path estimate | RoutePlanner.Zone estimates daily anchors followed by the known elite circuit for each entry. Current-player initial leg is included; unavailable/deferred points are excluded. EntryFor and Travel consume the calculated first-target entry. No elite search duration is invented. | Pure circuit/entry/current-leg/exclusion fixtures; actual controller BuildRoutes -> EntryFor and transport destination assertions |
| A single empty read removed a bill or changed generation | BillSnapshotGuard retains accepted counters and identity. Previously obtained bill removal/replacement requires two consistent observations at least 750 ms apart. Invalid/intervening readings reset confirmation. Current uncertain bill blocks automatic retargeting, flag writes and travel. | Empty/recovered/completed/replaced/restored/invalid-order/too-fast snapshots; actual controller counter/generation/goal/transport assertions |
| Shortcut proposal was not fully bound to contract | OrderId and Generation in offer identity, notification key, dialog validity and acceptance revalidation | Pure notification dedup/re-generation checks; actual stale dialog and no-transport acceptance checks |
| Reset union acknowledged categories no longer available | Each category independently revalidated at actual OnShown, including its current period | Queued union remains valid for weekly after daily pickup; only weekly is acknowledged |
| Settings shared route file; history write errors only logged | Independent versioned hunt-settings.json, atomic write, backup/quarantine, legacy migration. Persistent per-file warning and explicit retry; confirmed game credit unchanged | Independent corruption/backup/migration fixtures; real controller failed history write, retained completion, successful retry and warning clearance |
| Sounds combined | Independent EliteSound and NextZoneSound, both off for new settings. Old Sound preference migrates to both | Migration and independent-value persistence/backup assertions; both settings rendered |
| Empty states combined | Separate loading, no bills, completed, deferred, unavailable and filtered explanations/actions. Deferred/unavailable remainder also receives a banner while rows remain visible | Pure kind/action selection fixtures; six extra production UI compositions |
| Search was entirely suspended by navigation pause | Passive actor observation can lock/retain the elite without flag writes, point advancement or death-grace consumption. Other pause reasons survive | Pure passive observations and actual controller user+marker pause regression |

## Final local results

- Release build, warnings as errors: PASS (.NET 10, API 15).
- Route/state checks with real game sheets: seed 731 = 43,472; seed 1729 = 43,338; seed 9901 = 43,419. Assertions are not independent scenario counts.
- Map/data/portrait checks: 5,872, including 66 substituted-controller scenarios and 1,494 map-link round trips.
- Coordinate provenance verifier: 476 daily anchors; 77 target-specific elite sets / 922 point entries.
- Production UI: 20 main/mini compositions plus 4 native-dialog offline compositions. No horizontal overflow. Settings and persistent-save-warning images inspected locally.
- Local ZIP/manifest/DLL verifier: PASS. Public asset/index validation follows the release workflow.

## Remaining live acceptance

No game control was used. Check actual native flag acknowledgement and user flag ownership, NPC streaming/late detection, cross-zone and same-zone teleport/cancellation, native dialog focus and separate sounds in FFXIV. Client-loaded object detection and straight-line flight estimates retain their documented limits. ARR remains intentionally out of scope.
