# Hunt Route 0.4.0 — native gamepad interface

The default main window and mini panel use native FFXIV Atk nodes through the existing KamiToolKit initialization. There is one shared hunt controller, not a second route implementation. The mouse UI remains available explicitly as a fallback. World labels remain passive and do not capture input.

## Coverage

- Native hunt start/resume, pause, stop, current goal, map, next-zone/planned-trip actions and lost-elite release. Travel still requires the existing confirmation.
- Paged goal list, detail portraits, expansion/zone/complete/deferred filters, manual priority, target/zone deferral and return. Selection/filtering does not itself perform travel.
- Five boards with day/week status and native destination confirmation; login/reset pickers open native boards.
- Native settings for mini, reminders, sounds, independent overlays/ranges, shortcut limits, persistence retry, refresh and local diagnostics. Numeric editors provide small and large steps without typing.
- Directional navigation skips disabled and hidden actions. Focus follows semantic identity, not the previous row index. Own confirmations can restore focus to the previously focused Hunt Route window; passive updates do not take focus from another game window. Mini opening does not explicitly request focus. Closing presentation leaves the route active.

## Verified offline

- .NET 10 / Dalamud API 15 Release build, warnings as errors.
- 46,051 route/state assertions with real game sheets (seed 1729), including randomized enabled/disabled navigation graphs, reachability, page clamping, focus fallback and bounded layout slots.
- 5,936 map/data/portrait assertions, including 130 substituted-controller scenarios. Native screen building is read-only; explicit commands exercise the original controller, including start, pause, filtering, deferrals, independent settings, board confirmation, stale goals and close.
- 13 native-model layout compositions, including mini, pagination and long text, plus previous UI/dialog/overlay regressions. Native placement data is shared by runtime and the offline preview.
- Local package manifest/DLL/ZIP validation. Public release and custom-feed validation follows CI.

## Live acceptance still needed

No game control used; offline tests do not prove actual Atk input dispatch or physical controller focus. After updating: open `/huntroute`, use directional input to navigate tabs/goals/settings, select a goal without travel, confirm or decline a board/route teleport and check focus return, then close the window and confirm tracking continues. With mini enabled, verify it appears without stealing character input. `/huntroute classic` remains available if the native window cannot be used.
