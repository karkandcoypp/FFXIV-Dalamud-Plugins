# Eorzea Event Tracker 0.19.0

- Selected events now have a clean cover image and a prominent 1.65x heading below it, with a short gold accent and countdown. Removed the redundant Seasonal journal / Live text and text overlays on artwork.
- Clicking a reward icon or English item title opens its existing FFXIV Online Wiki page in the default browser. Exact title lookups follow wiki redirects and reject missing or disambiguation pages.
- Wiki checks are asynchronous, cached and performed only on click. Network failures display a retry message; missing pages do not open a browser. Only validated HTTPS item-page URLs on the wiki host are accepted.
- Previous releases remain available for rollback; character progress and preferences are unchanged.

Validation: Release build with warnings as errors, client/collector checks and live read-only wiki lookup. In-game rendering and browser interaction require player verification.
