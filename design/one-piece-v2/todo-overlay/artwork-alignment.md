# Todo mural alignment references

All 17 backgrounds use individually reviewed subject anchors. The complete original source crop is retained. Its larger distance from the anchor to either horizontal or vertical edge determines a uniform fit, leaving at least 4% horizontal and 6% vertical breathing room. No source image, controller crop, or saved artwork selection is modified.

Bounding the faint decorative ink previously centered clouds, tails and weapons instead of the subject. An ink-weighted center was also rejected after visual comparison: it moved Whitebeard's skull off its existing correct axis and overcorrected several emblems. These fixed references make layout independent of opacity, tint, texture thresholds and image sampling.

Coordinates below use the original 1254×1254 atlas, except Sun Warrior, which uses its separate 1206×1305 image. Vertical anchors retain the existing visible composition center. The horizontal anchor is visually calibrated to the identifying subject.

| Artwork | Subject center X, Y | Alignment reference |
| --- | --- | --- |
| Sun Warrior | 685, 647 | Figure and shield; the long sword must not pull the figure right |
| Dawn Dancer | 155, 180 | Dancing figure and sun, excluding faint peripheral curls |
| Sun Pirates | 468, 183 | Central sun disc |
| Kozuki Crane | 780.5, 182 | Circular crest axis retained |
| Shandora Bell | 1086, 182.5 | Bell and supporting frame |
| Cloud Dragon | 179, 484 | Dragon's main head and coiled body |
| Phoenix | 469.5, 484.5 | Balanced wings and body; existing axis retained |
| Zunesha | 759, 486.5 | Elephant and city, excluding peripheral clouds |
| Laboon | 1047, 484.5 | Whale's main body; the thin tail remains fully visible |
| Sea King | 146, 769.5 | Head and main coil |
| Straw Hat Crest | 476, 772.5 | Skull, hat and crossbones |
| Heart Pirates | 782, 768.5 | Circular emblem axis retained |
| Whitebeard | 1094, 774.5 | Approved skull/moustache axis retained |
| Kuja Serpents | 163, 1064 | Central skull and surrounding serpents |
| Three-Sword Spirit | 469.5, 1062.5 | Figure and balanced sword arrangement; existing axis retained |
| Ace's Flame | 792.5, 1062.5 | Full flame emblem; existing reviewed axis retained |
| Poneglyph | 1079, 1064 | Stone block, excluding surrounding cloud ornament |

`TodoMuralChecks` contains independent source-ink bounds and these manually reviewed subject landmarks. It recovers the native drawing's placement from its rendered pixels, then checks the subject position, rather than asking the renderer for its own center. Portrait, wide and enlarged panels, light/dark tint and 30% opacity are covered. A separate native-window assertion checks that the layer itself is centered within the expanded overlay.

Visual evidence is in [the panel gallery](implemented/artwork-audit/all-17-panels.png), [the guide gallery](implemented/artwork-audit/all-17-guides.png), and the 17 individual native panel captures beside them. Guides are test-only and never appear in the application. For revised artwork, review both galleries and update the source annotations rather than changing an opacity threshold until a test passes.
