# Modular frame catalog audit

Native design prototypes only. All 16 source PNGs remain unchanged. No new runtime artwork, production renderer changes, UI windows, hotkey registration or user settings access.

Every artwork placement uses uniform 0.18 scale; extra length uses repeated source strips with final-tile clipping. Fixed ornaments have separate placements. Passing geometry checks does not mean a seam or content layout is visually approved.

| Frame | Modular recipe | Minimum artwork size (DIP) | Review concern |
| --- | --- | --- | --- |
| Rope Binding | Fixed knots and repeated rope sections | 101.52 x 83.52 | Check braid phase and final clipped join at each knot. |
| Chart Scroll | Fixed roll caps; repeating paper roll and ragged edges | 101.52 x 83.52 | Paper texture joins may need a dedicated seamless strip; caps must never be elongated. |
| Royal Cartouche | Four fixed corners, four fixed crests, repeated rails | 217.8 x 130.32 | Top crest needs reserved header/ACTIVE-label space; side-crest joins need inspection. |
| Classic Brass | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Navy Bevel | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Walnut Rim | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Pixel Voyage | Fixed corners and repeating edge strips | 101.52 x 83.52 | Snap native geometry to whole device pixels at production DPI; no fractional pixel-art stretching. |
| Instrument Case | Fixed corners and repeating edge strips | 101.52 x 83.52 | Inspect source texture joins and content padding before production integration. |
| Slim Capsule | Fixed curved quarters; short connector strips | 132.84 x 130.32 | Quarter-curve joins and inner clipping need validation with real content. |
| Admiralty | Fixed fan corners and side diamonds; repeated rails | 101.52 x 130.32 | Side diamonds and corner fans require explicit content insets. |
| Bound Logbook | Fixed corners and repeating edge strips | 101.52 x 83.52 | Keep stitch spacing constant and conceal the partial repeat under corner fittings. |
| Cloud Crest | Fixed curved quarters; short connector strips | 195.84 x 130.86 | Quarter-curve joins and inner clipping need validation with real content. |
| Tide Curve | Fixed curved quarters; short connector strips | 132.84 x 130.32 | Wave phase at horizontal joins needs manual inspection; do not stretch the wave to hide a seam. |
| Engraved Filigree | Previously accepted fixed ornaments and repeated rails | 104.4 x 106.92 | Accepted visual direction; production integration remains pending. |
| Open Corners | Four fixed brackets; intentionally no connecting rails | 101.52 x 83.52 | Keep the frame open; content clipping must not depend on a closed raster outline. |
| Vine Filigree | Fixed curved quarters; short connector strips | 158.4 x 130.32 | Leaf-to-rail joins need a tailored connector; top and bottom seals stay fixed. |

## Evidence

`verification.json` records all 48 size recipes and their exact source/destination regions. The three complex examples also render at 144 and 192 DPI off-screen; final review boards use 96 DPI. Minimum sizes describe fixed-artwork clearance only, not usable content minimums. No performance claim is made.

Reproduce after building the harness with `Run-HiddenDesktop.ps1 -Mode '--frame-catalog-audit'` and a repository-local log path. Native fixtures run on a private desktop; no active-desktop interaction.
