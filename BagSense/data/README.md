# BagSense item database

Public FFXIV game data from [xivapi/ffxiv-datamining](https://github.com/xivapi/ffxiv-datamining), English locale. Game data and text remain the property of SQUARE ENIX CO., LTD.

`manifest.json` identifies the source Git commit, timestamp, schema, counts and SHA-256 of a content-addressed gzip. It contains every named Item row plus conservative RITEM quest associations and hand-in evidence. It contains no player data or executable code.

The daily `bagsense-data.yml` workflow rebuilds it from pinned upstream files. Unchanged sources produce identical output. Current and previous payloads remain available to avoid CDN manifest/payload races. The plugin checks on load and daily and preserves a verified local cache if the network fails.

A full item catalog does not imply complete quest-script coverage. Only literal unambiguous item-count phrases are accepted; ToDoQty is never a hand-in count. Unknowns remain explicit, and active unknown requirements protect all stock.
