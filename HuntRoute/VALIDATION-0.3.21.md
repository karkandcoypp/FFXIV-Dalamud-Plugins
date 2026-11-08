# Hunt Route 0.3.21 — start from Kugane

## Reproduced failure

The user's saved state contained obtained Stormblood daily bills 6/7/8, an inactive journey and an old Heavensward target. A substituted-controller test loads real game-sheet targets for Stormblood orders 2/3/5, starts in Kugane (territory 628/map 370) and retains a previously Finished HW session. Before the fix, ResumeJourney queued a travel request without replacing the terminal session; AdvanceRoute then marked the journey inactive again and invalidated the proposal before display. The new regression failed on 0.3.20 and passes after the fix.

## Fix

PlanTravel establishes the new target/run before confirmation and uses explicit AwaitingTravel. This differs from WaitingForZone, which means a teleport was accepted or arrival is expected. Later leaves a visible reoffer action without automatic duplicate dialogs. Rejected requests remain unconfirmed and cannot create pending arrival. Explicit acceptance retains the same run and arrival flags the first valid goal. Credit arriving before confirmation reconciles the plan without submitting transport.

The target-list return button is now Back, with a separate Start Hunt action. Inactive stale session phases cannot produce a Pause button that does nothing. A stale availability cache no longer hides the explicit retry, and failed starts have visible feedback.

## Checks

- Warnings-as-errors Release build.
- 100 integrated controller scenarios / 5,906 map-data-portrait assertions, including the reproduced city start, fresh run/target, no pre-confirmation cast, pause, Later/no repetition, explicit reoffer, game rejection, exact one accepted cast and automatic first marker after arrival.
- 43,363 route/state assertions with real game sheets (seed 1729), including planned-state persistence across ticks and manual arrival.
- Existing overlay regressions retained from 0.3.20; three UI states added for planned travel, inactive stale phase and stale availability.
- No game control used. Live input focus, native popup visibility and teleport timing still require in-game acceptance.

The native gamepad-compatible main UI is a separate requested discussion; this release does not claim to convert the ImGui main window to native game nodes.
