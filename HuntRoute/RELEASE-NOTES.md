# Hunt Route 0.3.3

- Align all five hunt-board markers with the actual game Level objects in Foundation, Kugane, the Crystarium, Old Sharlayan and Tuliyollal. Keep each city's verified aetheryte/territory/map IDs.
- Reject mismatched map/territory pairs before selecting a route entry point.
- Keep profitable intra-zone teleport offers queued while the character is busy; a transient busy frame no longer consumes the only offer. No/close still declines it.
- Dispose closed native confirmation addons explicitly after callbacks instead of leaving them to garbage collection.
- Expand data, route-builder, unlocked-aetheryte, same-zone and randomized route regression checks; see AUDIT.md for source object IDs and validation limits.

Public package/feed verification and local tests do not replace live-game checks of casting, loading, markers and native UI.
