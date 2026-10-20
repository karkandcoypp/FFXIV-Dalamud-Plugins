## 0.6.0 source graph

Collector version 7 adds pinned Teamcraft enemy/duty/FATE/container acquisition facts and input associations, plus reviewed COST_ITEM_ATMA/COST_USE_ATMA semantics. Shared-map NPC positions use the canonical map territory. Current payload: 54,411 records, 4,800 quest-item links, 693 confirmed quantities and 20,630 additional acquisition links. Each daily run pins current ffxiv-datamining and Teamcraft commits in manifest.json. Counts are evidence coverage, not a claim that every item or server-side condition is fully understood. See ../coverage-audit.json.

The preceding published payload is retained during this release so readers of its cached manifest can finish downloading.

# BagSense item database

Public FFXIV game data from [xivapi/ffxiv-datamining](https://github.com/xivapi/ffxiv-datamining), English locale. Game data and text remain the property of SQUARE ENIX CO., LTD.

`manifest.json` identifies the source Git commit, timestamp, schema, counts and SHA-256 of a content-addressed gzip. It contains every named Item row plus conservative RITEM quest associations and hand-in evidence. It contains no player data or executable code.

The daily `bagsense-data.yml` workflow rebuilds it from pinned upstream files. Unchanged sources produce identical output. Current and previous payloads remain available to avoid CDN manifest/payload races. The plugin checks on load and daily and preserves a verified local cache if the network fails.

A full item catalog does not imply complete quest-script coverage. Only literal unambiguous item-count phrases are accepted; ToDoQty is never a hand-in count. Unknowns remain explicit, and active unknown requirements protect all stock.
