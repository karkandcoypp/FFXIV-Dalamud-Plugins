# Hunt Route 0.3.4

- Replace zone-wide weekly search points with target-specific sets: 77 B-ranks, 47 territories and 922 point entries. Validate target ID, map and territory; never show another mob's zone fallback.
- Review all 476 daily anchors against current XIVDaily data and individual FFXIVHunt observation maps. Alternative valid packs are preserved; no confirmed daily coordinate error remained. Source revisions and review evidence are documented in COORDINATE-AUDIT.md.
- Fix completed daily bills displaying as not taken after they disappear: validate retained order IDs and all kill requirements against the current available set.
- Show a green check, `Выполнено сегодня · 3/3` and a clear daily-completion message. Weekly completion remains separate. Partial/discarded bills do not count; new available sets clear completion.
- Add offline coordinate-source verification, per-target weekly map checks and compact completed-state UI regression cases.

Public package/feed and local tests are verified separately from live-game behavior. Positions are community observations, not guaranteed current sightings.
