# Hunt Route

Dalamud API 15 hunt assistant for Heavensward, Stormblood, Shadowbringers, Endwalker and Dawntrail. Based on HuntBuddy's coordinate reference; ARR remains outside the supported route/dashboard.

## Start here

Open `/huntroute`. The first page summarizes daily and weekly bills across all five expansions. Use **Start hunt** for a unified route, or select a mob and explicitly choose **Track this target** for manual priority. Clicking a name only selects it; hovering shows its game portrait. Automatic tracking never opens the map: use **Map** yourself.

- Within the current zone, tracking starts without teleport. Cross-zone travel requires the native confirmation. After arrival, the first needed flag and following resume automatically.
- The route uses unlocked entries and estimated remaining flight length. Terrain and altitude are approximate; this is not navigation mesh routing. Hinterlands retains the Idyllshire gateway.
- Nearby loaded ordinary targets can replace static anchors. The selected instance stays selected until death or loss of visibility; game kill counters remain authoritative. A manually selected goal cannot be preempted by automatic discovery.
- In automatic mode, a discovered needed weekly elite takes priority. Otherwise its target-specific possible spawns are visited on fly-by within 45 horizontal yalms, with no landing or dwell timer. Rounds repeat automatically; nearby/single-point repeats require leaving and returning rather than churning on the spot. One reminder follows three completed rounds.
- Elite instance locks survive visibility loss. Observed death waits three active seconds and fresh counter readings; no credit resumes search, late credit stops it. A new living instance can lock immediately. Missing visibility alone never proves death; the explicit release button is available for lost targets.
- Search polls at 100 ms, contracts at one second. The detector only sees client-loaded objects and cannot guarantee an empty area. World-label distance affects labels, not the detector's ability to use loaded objects.

## Pause, restore and deferred targets

Pause reasons are independent. An external map flag pauses route marker writes and travel suggestions without stopping credit tracking. Use **Return tracking** to hand the flag back. Manual zone changes adopt local goals in automatic mode; a manually prioritized remote goal instead stays paused. Closing the main window does not stop following.

The optional mini panel keeps the goal, counters, status, map and pause controls visible. Enable it in Settings or use `/huntroute mini`. Commands `/huntroute pause`, `/huntroute resume`, and `/huntroute stop` act on the route without needing the main window.

Unsupported positions/entries are deferred with reasons; explicit manual deferrals require returning them. Deferred objectives are never claimed completed. Stop ends the route; Pause keeps it. Visiting a board preserves the route on a service pause.

Settings are independently stored in `hunt-settings.json` (legacy combined settings migrate automatically). Route state is saved per character in `journey-state.json` and restores paused, with contract revalidation and no automatic teleport. Object identities and pending casts are not carried across game sessions/world/instance contexts. Atomic writes, a backup and quarantined corrupt files protect state. Existing `completion-history.json` remains separate: daily/weekly checks persist per character/tier and expire at their UTC reset boundary.

## Reset notices and teleport suggestions

After each character login and fresh game data, an idle/paused character gets one hunting proposal, independently of prior reset acknowledgements: continue held bills, select a board for new bills, or open the summary when no new goals are available. Later suppresses this login offer until the next login. While staying logged in, reset offers remain once per daily/weekly period. During active hunting only a passive notice appears. The five-expansion board overview separates daily and weekly availability. Selection requires a second travel confirmation. Already in the city means only a board flag. Bills are never taken or discarded automatically. Later retains the overview action without repeated popups for that period.

Long-flight shortcut settings default to <=200 gil, >=10 seconds and >=20% savings, with >=30 seconds original flight. Cast/load overhead is measured for plugin trips when possible, otherwise estimated. A zero budget permits free shortcuts only. Flight-based suggestions require current flight capability; inter-zone confirmations are outside the shortcut budget. Price, entry, target and route identity are rechecked at acceptance.

Cancelled/rejected casts and missing/incorrect arrivals are explicit states, never automatic re-casts. Another manual destination adopts or pauses according to the current mode. Goal credit during travel is reconciled at arrival instead of reflagging a completed target.

Separate elite-detection and next-zone sounds default off. Debug export is local and bounded; nothing is uploaded automatically.

Bill removal/replacement is confirmed by two consistent reads at least 750 ms apart; an uncertain current bill retains its goal and blocks navigation until refreshed. Shortcut proposals include the full contract generation. Failed saves keep a visible warning and an explicit retry action without changing game completion.

## Validation

```
dotnet run --project HuntRoute/tests/RouteChecks -c Release
dotnet build HuntRoute/HuntRoute/HuntRoute.csproj -c Release --warnaserror
dotnet run --project HuntRoute/tests/MapChecks -c Release -- "<game/sqpack>" HuntRoute/data/elite-spawns.json
dotnet run --project HuntRoute/tests/UiChecks -c Release -- "<preview directory>"
```

MapChecks uses real game sheets and substituted clock/transport/flag services for controller scenarios. It verifies map links, boards, portrait availability, instance death/credit, route continuation, deferrals, resets, casts and restoration without controlling the game. UiChecks renders offline compositions at different sizes, including mini/settings/selection/elite states. These checks do not replace live FFXIV NPC streaming, native UI, sound or teleport testing.

## Sources

- HuntBuddy coordinate reference: https://github.com/SheepGoMeh/HuntBuddy/tree/1de61e25b99b6473f4d2c75b4023e588016f0aa4
- Current target-specific B-rank positions (MIT): https://github.com/RKI027/ffxiv-huntmaps-maker/tree/047ac3fddf28eb3232c59ed7006d085584963a9d
- Historical zone-wide candidate positions through 0.3.3 (MIT): https://github.com/img02/HuntHelper/tree/0c07d87a1e951f9dce7a17e500d41bbc752bee94
- Umbra was inspected as a behavior reference for client-visible world markers; no Umbra code was copied: https://github.com/una-xiv/umbra/tree/4f7abbd045e7769ec01ef6456dba0d72dee3c2da
- Reset schedule reference: https://ffxiv.consolegameswiki.com/wiki/Reset

The target-specific huntmaps MIT notice is included as `Huntmaps-LICENSE.txt`; the earlier HuntHelper notice is retained as `HuntHelper-LICENSE.txt`. Native UI uses KamiToolKit (MIT), pinned at `b4b7cab18e3b60abb49391272acb421e35b52bad`; its source is vendored and `KamiToolKit-LICENSE.txt` is included in the package. See [third-party notices](THIRD-PARTY-NOTICES.md).

Coordinate review, source revisions and limitations: [COORDINATE-AUDIT.md](COORDINATE-AUDIT.md). Verify the audited snapshot with `python HuntRoute/tools/verify_coordinate_data.py`; optionally pass `--huntmaps <upstream data/marks.json>` to compare all target assignments to the pinned source.

Travel confirmations share a measured layout: native text measurement sizes the real game addon; offline composition previews cover next-zone, intra-zone and long-name cases. Intra-zone offers display quoted gil cost and estimated direct/teleport/saved time in separate cards. The game frame/button textures may vary with the selected game theme; offline previews are not live Atk screenshots.
