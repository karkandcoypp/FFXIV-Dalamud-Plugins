# Clean automatic-event integration check

`auto-fallguys.json` is an isolated, automatically generated snapshot for testing the current plugin. It is not the normal subscription feed.

Collected from the official October 2026 Fall Guys announcement using an explicitly empty overrides file, a new output directory, an empty translation cache and no reviewed glossary. The public Lodestone and XIVAPI responses supplied the quests, rewards, icons, prices and NPC destinations; MyMemory supplied the Russian text. Missing data was not filled by hand.

Only the event identity was prefixed with `e2e-auto-` after collection, so an existing hidden-event preference or bundled event mapping cannot mask the test. Tracking IDs were changed to match that test identity. Item IDs, quest IDs, dates and prices are unchanged. After the in-game test exposed an inaccessible merchant map, the generic entrance-routing stage replaced the merchant map destination with Wy in the Gold Saucer. The original merchant destination remains in navigation metadata, and bilingual routing notes explain the correction. See testProvenance in the snapshot.

Observed collection result: 23 rewards; 22 prices; 6 NPC destinations; 21 of 23 reward names translated. Gentlebean Bottoms and Blunderville Rainbow stayed English. Wide Spectrum #1 Dye has an unresolved exchange and no inferred price/map destination. This is a genuine partial result, not a manually completed fixture.

The local read-only installed-game check confirmed all 23 item IDs/icons, all 22 prices, all 6 NPC identities/maps/coordinates, and the five quest issuers against the installed FFXIV game tables.

## Reproduce client loading

From the repository root, run with the bundled .NET runtime or an installed .NET 10 SDK:

```powershell
dotnet run --project EorzeaEventTracker/tests/FeedChecks/FeedChecks.csproj -c Release -- --integration-url https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/EorzeaEventTracker/tests/live/auto-fallguys.json client-report.json
```

This uses the actual EventFeedService, FeedPolicy, Localizer, FavoriteBudget and EventImages source. Its bundle/cache start empty; there is no bundled event fallback. It downloads the feed and artwork over HTTPS and checks offline restart. The currency arithmetic check explicitly uses a simulated balance of 100; it does not claim to read the character's live balance.

Optional read-only installed-game check on a Windows machine with Dalamud development files:

```powershell
dotnet run --project EorzeaEventTracker/tests/ApiChecks/ApiChecks.csproj -c Release -- --installed-data 'C:/path/to/FFXIV/game/sqpack' EorzeaEventTracker/tests/live/auto-fallguys.json
```

## Finish in FFXIV

1. Record your existing HTTPS feed URL in plugin settings. Select Russian and enable partial event entries.
2. Temporarily set HTTPS feed URL to the raw auto-fallguys.json URL above and press Save feed URL. Open the journal manually with /events.
3. Check the Russian event title, reward icons/prices and cover. Click Where to go for Lewena (The Gold Saucer X:4.8 Y:6.1), then a normal paid reward (Wy, The Gold Saucer X:6.1 Y:7.4; speak to Wy to enter Blunderville Square, then visit the MGF trader inside at X:3.8 Y:3.1). Verify the correct map and flag open. Mark an unowned priced reward as favorite to compare the shown MGF balance with the game.
4. Restore the previously recorded feed URL and any settings/preferences changed during the check. The standard URL is https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/EorzeaEventTracker/config/events.json . Do not leave the frozen snapshot as your permanent feed: it will expire with the real event.

The automated diagnostics do not control the game process and cannot verify rendering or an actual in-game click. Those steps remain explicitly manual. No plugin DLL update is required for this test.

Name policy update: from plugin 0.15.0 and the refreshed test feed, item and NPC names are intentionally English. The earlier 21/23 Russian item-name count above records the historical automation test, not the current expected presentation. Russian event/quest titles, descriptions and UI remain supported.
