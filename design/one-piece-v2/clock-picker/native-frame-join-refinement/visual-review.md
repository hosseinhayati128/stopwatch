# Frame join refinement

Native prototype review, 2026-11-05. The user approved refining the four priority recipes identified in the catalog audit. The original audit boards remain in `../native-frame-catalog-audit/` for comparison.

## Changes and observations

| Frame | Change | Observed result at 420 x 132, 460 x 480 and 820 x 650 |
| --- | --- | --- |
| Tide Curve | Complete half-waves are paired with their horizontal reflection. Level trough strips absorb remaining width; a short opacity blend connects to the existing right ornament. | The abrupt mid-wave breaks are gone. Wavelength, thickness and scroll size stay fixed; larger panels add complete waves and level sections. |
| Admiralty | Separate fan tips from the outward diamond flare; assemble straight side rails from the existing inner and outer strips. Center the complete diamond section. | The stray corner protrusions are gone. Fans meet straight rails, and side diamonds stay centered and fixed in size. |
| Vine Filigree | Move the source cut between complete curls; extend the rails beneath a short blended stem exit. | The upper curls remain intact. The lower corners retain the formerly bisected curl and join the rails more smoothly. |
| Royal Cartouche | Move cuts between curls, retain a taller complete side crest, and overlap the rails beneath the ornament exits. | Corner endings and side-crest connections are smoother; all crests keep their scale and positions. |

The four `*-before-after.png` boards compare the original and refined picker at the same size. The `*-three-sizes.png` boards show the new recipes at all three fixture sizes. These are review artifacts, not final picker styling or user approval.

## Verification

- Harness build passed with zero warnings and errors.
- All 16 recipes rendered at all three sizes: 48 samples.
- 6,223 source/destination placements passed equal-axis scale and source-bound checks.
- All source hashes remain unchanged; no new runtime artwork files.
- The four refined frames rendered at 1.5x and 2x resolution for 24 additional comparisons. Alpha-coverage agreement with each 1x baseline was at least 0.77 (guard threshold 0.70); antialiasing accounts for some pixel differences. The guard detects large missing regions and displaced content; it does not judge seam quality.
- All four three-size boards and before/after boards were visually reviewed. Tide Curve and Royal Cartouche were also inspected at 2x resolution.
- Native runs used a private desktop; no active-desktop interaction, app windows, hotkeys or settings access.

## Capture limitation and production follow-up

Inspecting the high-resolution exports exposed an offscreen capture problem: non-96-DPI `RenderTargetBitmap` targets double-scaled text and misplaced transformed image pieces. Drawing directly into the target did not resolve it. The harness now applies export scale once in the drawing context while retaining a 96-DPI target. This produces native 1.5x/2x renders, rather than enlarging a finished raster. The `144dpi` and `192dpi` filenames denote equivalent pixel dimensions; their PNG metadata remains 96 DPI. Physical monitor DPI and the live application's DPI path have not been validated by this prototype.

The source artwork contains some small edge-color artifacts visible at 2x. Those remain source quality issues, separate from the modular joins. Texture seams in rope, paper, wood and stitched borders still warrant a later close inspection.

Before production integration, define per-style content insets and background masks, validate small sizes and supported display DPI, and measure rendering cost. Tide Curve's level sections currently use many one-pixel source strips; a cached repeat brush or cached assembled border could reduce draw calls without adding bundled artwork. No performance claim is made here.

The Cloud Crest minimum-size guard now uses the crest-clearance minimum before constructing pieces; minimum-size boundaries are otherwise outside this review. The installed application and production renderer remain unchanged.
