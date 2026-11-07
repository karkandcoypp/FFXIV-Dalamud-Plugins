# Hunt Route 0.2.0

- Follow daily hunts: click a target, arrive, then automatically flag the next target after meeting its kill count. The next zone is suggested when the current zone's daily goals finish; every zone teleport requires a click.
- Suppress all same-territory teleports, including target, route start and board actions. Add busy/loading/casting/pending checks, a repeat-request guard and locked-destination validation.
- Recalculate remaining routes after completion, starting from the character's position when already in the zone. Group coincident target positions and handle oversized routes without blocking the plugin.
- Show game-provided hunt portraits when hovering daily or weekly names.
- Compact adaptive window, minimum 520x430; narrow windows use a zone dropdown. Highlight the tracked target and keep route controls visible.
- Audit fixes: unified map/texture/aetheryte projections, validated contract ID ranges, ignored transient zone-loading snapshots, handled missing map markers, and invalidated replaced weekly selections.

Local validation: 119 logic checks, 2806 installed-data/wrapper checks (including 1495 map-link round trips, 47 map projections and 794 portrait assets), and five rendered UI scenarios. Actual in-game native teleport, automatic flags and hover texture rendering remain to be checked.
