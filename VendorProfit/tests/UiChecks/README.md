# Standalone production UI preview

Build VendorProfit first, then run this project with an output directory. Requires Windows, .NET 10 and the local Dalamud API 15 SDK. Run `python render.py <output-directory>` with NumPy and Pillow to produce PNGs.

The harness invokes the production window renderer without the plugin constructor, game services, worker or purchase gateway. Text/number inputs are inert substitutes because Dalamud native input requires its host. Covers four tabs, 850×520 at 16px and 1100×720 at 21.6px. Sample lots are fictional layout fixtures. Does not replace in-game interaction checks.
