# Hunt Route 0.3.19 — focused interface

Reviewed on 2026-11-09. The goal is a quieter in-game tool, rather than a dashboard that exposes every feature simultaneously. No external UI assets or frontend framework were imported.

## Design references

- [NN/g: Progressive Disclosure](https://www.nngroup.com/articles/progressive-disclosure/): frequent tasks first; advanced/rare features on request. This is an established usability principle, not a newly invented 2026 trend.
- [Microsoft Fluent 2: Layout](https://fluent2.microsoft.design/layout): grouping through proximity, a consistent spacing rhythm, hierarchy and responsive layouts. Applied to the native ImGui constraints rather than copying web styling.

## Information architecture

- **Hunt**: current target, zone, kill count, progress, one contextual primary action, Map and More. Idle state offers Start or board selection. All targets opens a separate route view.
- **All targets**: collapsible zone groups; filters closed initially; name selection reveals tracking/map/deferral actions. Viewing never starts a teleport. An explicit focus action reveals the target; automatic tracking does not switch the page.
- **Boards**: five expansion cards with daily/weekly completion and an explicit board action. This is also where login/reset board-selection requests lead. No expansion table on the hunt screen.
- **Settings**: main preferences first; sounds, shortcut thresholds, world-label range and maintenance disclosed separately.
- **Mini**: the same compact target card and primary action; stop/manual-priority cancellation/lost-elite release in More.

Opaque neutral surfaces preserve legibility over a busy game scene. A mint primary action carries emphasis; statuses include text. Stable scrollbar space avoids width jumps between short and long screens. Narrow windows/large fonts switch navigation to a section selector and stack action rows as needed. Main minimum width is 400 px, mini 300 px.

## Preservation and verification

Route planning, counters, elite lifecycle, flag ownership, persistence and travel code are unchanged by this refactor. UI request flags are consumed by the view; board eligibility is still revalidated by existing backend confirmations.

- Release build with warnings as errors.
- 35 production UI compositions, widths 300–1180, font sizes 17/20/26; horizontal-overflow check, explicit board/focus navigation checks and a before/after journey-state comparison during render.
- Four existing offline native-dialog previews retained; native confirmation layout/behavior unchanged.
- 78 substituted-controller scenarios, 5,884 map/data/portrait assertions, 43,338 route/state assertions (seed 1729 with real game sheets).
- Offline previews inspected for current hunt, boards, settings and narrow mini panel. These are not screenshots from FFXIV.

No game control used. Real input focus, game font/DPI behavior, native streaming and travel still need in-game acceptance. Distribution follows the existing tag/release/feed verifier.
