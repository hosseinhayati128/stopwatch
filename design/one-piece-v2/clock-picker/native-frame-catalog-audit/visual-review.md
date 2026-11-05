# Modular frame visual review

Reviewed 2026-11-05 using the native three-size boards and four catalog sheets. This records the first catalog audit, not user approval or production readiness.

## What the audit establishes

All 16 existing frame PNGs were reused at 420 x 132, 460 x 480 and 820 x 650 DIP. The harness checked 4,112 placements for equal horizontal and vertical scale (0.18) and valid source bounds. Length is added by repeating strips and clipping the final repeat. Corners and ornaments retain their size and proportions. Source hashes are unchanged; no runtime artwork files were added.

Rope Binding, Chart Scroll and Royal Cartouche also rendered at 144 and 192 export DPI, for 18 additional renders. These are offscreen export checks, not physical monitor DPI or production performance tests. All fixtures ran on a private desktop without app windows, hotkeys or settings access.

## Visual findings

| # | Frame | Observation | Remaining work |
| --- | --- | --- | --- |
| 1 | Classic Brass | Corner size and border thickness remain consistent at all three sizes. | Inspect subtle repeated highlight/texture joins at device scale. |
| 2 | Navy Bevel | Overall bevel geometry stays consistent; no obvious break at board scale. | Validate shading joins and interior clipping with the chosen background. |
| 3 | Walnut Rim | Wood corners retain their proportions; repeated rails preserve grain scale. | Check grain discontinuities at tile boundaries. |
| 4 | Pixel Voyage | Stepped corners and border proportions are retained. | Establish whole-device-pixel alignment at supported DPI values. |
| 5 | Instrument Case | Corner fittings and rails remain consistent across sizes. | Check material seams and content clearance. |
| 6 | Slim Capsule | End curves retain their radius as the panel grows taller. | Validate curve-to-straight joins and the interior mask with real content. |
| 7 | Rope Binding | Knots keep their size; rope twists are not elongated. | Tune braid phase where the final clipped segment meets a knot. |
| 8 | Chart Scroll | Roll caps retain their width and shape; longer paper sections repeat. | Repeated paper texture can create periodic bands; refine texture joins without stretching the caps. |
| 9 | Admiralty | Side diamonds stay centered and fixed in size. Tall versions expose abrupt cuts beneath the upper corner fans. | Tailor the fan-to-rail transitions and reserve space around diamonds. |
| 10 | Bound Logbook | Corner studs and stitch scale remain consistent. | Align stitch phase and conceal partial repeats near fittings. |
| 11 | Cloud Crest | Cloud ornaments and central arches retain their shapes. No obvious break at board scale. | Validate curved clipping and minimum-size behavior; source dimensions differ from the other frames. |
| 12 | Tide Curve | Wave shapes retain their proportions, but repeats visibly break at top and bottom joins, including near the right ornaments. | Required before adoption: choose phase-matched wave segments and join them to fixed ornaments without stretching. |
| 13 | Engraved Filigree | Reproduces the previously accepted direction: fixed corner and side ornaments with repeated rails. | Validate production content insets, masking and DPI. |
| 14 | Open Corners | Four brackets stay fixed; the open sides are intentional. | Define background clipping independently of a closed raster outline. |
| 15 | Vine Filigree | Leaf clusters and center seals stay fixed. Tall versions expose flat cuts where upper leaves meet the rails. | Tailor leaf-to-rail connectors and content insets. |
| 16 | Royal Cartouche | Top, bottom and side crests retain their scale and placement. | Smooth abrupt ornament/rail transitions and reserve header space beneath the top crest. |

## Next refinement pass

Start with Tide Curve's visible wave discontinuities, followed by Admiralty, Vine Filigree and Royal Cartouche ornament joins. Compare the revised recipes at these same three sizes before moving the component into production. Texture seams in rope, paper, wood and stitched borders need a subsequent close inspection.

Usable minimum window sizes, backgrounds, clipping, header/ACTIVE placement and picker interaction states remain separate design work. The minimum sizes in the generated manifest describe artwork clearance only; minimum-size boundary behavior has not been validated. Cloud Crest's prototype currently assigns its wider crest-clearance minimum after the initial size guard, so that guard must move before testing small sizes.

The existing application renderer and installed build have not changed. Review PNGs in this directory are design artifacts, not bundled frame assets.
