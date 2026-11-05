# Modular frame catalog audit

Production shared-renderer audit using the reviewed recipe metadata. All 16 source PNGs remain unchanged. No new runtime artwork, UI windows, hotkey registration or user settings access.

Every artwork placement uses uniform 0.18 scale; extra length uses repeated source strips with final-tile clipping. Fixed ornaments have separate placements. Passing geometry checks does not mean a seam or content layout is visually approved.

| Frame | Modular recipe | Minimum artwork size (DIP) | Review concern |
| --- | --- | --- | --- |
| Rope Binding | Fixed knots and repeated rope sections | 101.52 x 83.52 | Check braid phase and final clipped join at each knot. |
| Chart Scroll | Fixed roll caps; repeating paper roll and ragged edges | 101.52 x 83.52 | Paper texture joins may need a dedicated seamless strip; caps must never be elongated. |
| Royal Cartouche | Four fixed corners, four fixed crests, repeated rails | 217.8 x 130.32 | Overlapping rails soften ornament exits; reserve header/ACTIVE-label space under the top crest. |
| Classic Brass | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Navy Bevel | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Walnut Rim | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Pixel Voyage | Fixed corners and repeating edge strips | 101.52 x 83.52 | Snap native geometry to whole device pixels at production DPI; no fractional pixel-art stretching. |
| Instrument Case | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Slim Capsule | Fixed curved quarters; short connector strips | 132.84 x 130.32 | Quarter-curve joins and inner clipping need validation with real content. |
| Admiralty | Fixed fan corners and side diamonds; repeated rails | 101.52 x 130.32 | Fan tips are separated from the diamond flare; side ornaments require explicit content insets. |
| Bound Logbook | Fixed corners and repeating edge strips | 101.52 x 83.52 | Keep stitch spacing constant and conceal the partial repeat under corner fittings. |
| Cloud Crest | Fixed curved quarters; short connector strips | 195.84 x 130.86 | Quarter-curve joins and inner clipping need validation with real content. |
| Tide Curve | Whole reflected wave repeats; level trough connectors | 254.16 x 130.32 | Wave length and thickness stay fixed; extra width is shared between level trough sections. |
| Engraved Filigree | Previously accepted fixed ornaments and repeated rails | 104.4 x 106.92 | Accepted visual direction; production integration remains pending. |
| Open Corners | Four fixed brackets; intentionally no connecting rails | 101.52 x 83.52 | Keep the frame open; content clipping must not depend on a closed raster outline. |
| Vine Filigree | Complete upper leaf curls; overlapping stem connectors | 158.4 x 130.32 | Stem cuts move between curls, with feathered overlap onto fixed-width rails; validate content clearance. |

## Evidence

`verification.json` records all 48 size recipes and their exact source/destination regions. The four refined frames also render at explicit 1.5x and 2x resolution, equivalent to 144/192 DPI pixel dimensions. PNG metadata remains 96 DPI. This avoids double-scaling observed in the non-96-DPI offscreen capture path; it does not validate physical monitor DPI. Pixel coverage is compared with the 1x baseline to catch missing borders or displaced text. Final review boards use 1x resolution. Minimum sizes describe fixed-artwork clearance only, not usable content minimums. No performance claim is made.

Reproduce after building the harness with `Run-HiddenDesktop.ps1 -Mode '--frame-production-audit'` and a repository-local log path. Native fixtures run on a private desktop; no active-desktop interaction.
