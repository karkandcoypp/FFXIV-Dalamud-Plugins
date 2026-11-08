# Hunt Route 0.3.14 verification

Implementation follows the agreed staged plan: actor lifecycle; unified route/deferrals; independent pauses, travel/recovery; overview/mini/settings/reset notices.

## Local checks

- .NET 10 / Dalamud API 15 Release build, warnings as errors.
- RouteChecks with game sheets and seeds 731, 1729, 9901: deterministic map/contract/reset/round logic and randomized instance-lock traces. Lifecycle checks add dead-without-credit, fresh/stale revisions, pause gaps, per-character atomic saves, backup recovery and uncertain-history behavior.
- MapChecks: 476 daily anchors, 77 target-specific elite sets, 922 point entries, 794 game portrait files, 1494 map-link round trips, board/gateway destination validation.
- 46 integrated journey-controller assertions use the actual plugin methods with injected clock, transport and flag services. Cases include ordinary actor selection, manual priority, elite death/credit, cross-zone arrival, external flags, independent pauses, deferrals, contract-generation replacement, cancel/timeout, already-in-city boards, Idyllshire gateway, persisted restore, reset-offer races and portrait fallback.
- UiChecks: 14 overview/following/selected/settings/elite/mini states across widths 360-1180 and font sizes 17/20, plus four native-dialog offline compositions. No horizontal overflow. Overview and mini images inspected locally.
- Source-coordinate provenance verifier and release ZIP/manifest/DLL/index verifier.

## Distribution checks required after CI

Tag `hr-v0.3.14`; release workflow; public ZIP integrity/checksum; both canonical and legacy raw pluginmaster links; unrelated index entries unchanged.

## Runtime boundary

No live FFXIV control was used. Substituted transport/flag services do not prove native AgentMap acknowledgement timing, real NPC streaming, UI focus, sound, teleport cancellation or instance transitions. Those remain the in-game acceptance checklist. Search is limited to client-loaded objects and straight-line estimates; it does not automate walking, flying, fighting, taking/discarding bills or switching instances.
