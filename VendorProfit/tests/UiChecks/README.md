# Standalone production UI preview

Build VendorProfit first, then run this project with an output directory. Requires Windows, .NET 10 and the local Dalamud API 15 SDK. Run `python render.py <output-directory>` with NumPy and Pillow to produce PNGs.

The harness invokes the production window renderer without the plugin constructor, game services, worker or purchase gateway. Text/number inputs are inert substitutes because Dalamud native input requires its host. Covers all five sidebar pages at 980×650 and 850×520 / 16px, and 1100×720 / 21.6px, plus active purchases, empty results and an error plus statistics data, selection and an empty history and current-world priority (21 previews). Asserts restoration of shared theme colors and padding. Sample lots are fictional layout fixtures. Does not replace in-game interaction checks.
