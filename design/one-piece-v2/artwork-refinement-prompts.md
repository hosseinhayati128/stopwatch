# Artwork refinement, 2026-10-27

Mode: built-in imagegen. Current reference: `ChatGPT Image Oct 26, 2026, 08_58_23 PM.png`, supplied by the user. Its unchanged copy is embedded as `StopwatchOverlay.Desktop/Assets/Nika/controller-art-reference.png` for carefully bounded header-art and crest regions.

Final generated assets:

- `StopwatchOverlay.Desktop/Assets/Nika/log-pose-reference.png`
- `StopwatchOverlay.Desktop/Assets/Nika/warrior-reference-flat.png`

The original generated outputs remain under the Codex generated-images folder. The first mural pass had unwanted brown glow and is retained only under `artwork-sources/warrior-reference-first-pass.png`; it is not embedded in the app.

## Log Pose extraction prompt

Use case: background-extraction. Asset type: transparent production UI artwork. Precisely isolate ONLY the small glass Log Pose compass from the upper-right workspace of the supplied approved screenshot (the object centered around x1170,y190, bounding approximately x1088-1254 and y122-257 in the 1536x1024 reference). Preserve that SAME original object's softly illustrated rendering, muted pale sea-glass teal and ivory, red/cream needle, rounded clear dome, aged warm brass/bronze base, original three-quarter perspective and proportions. It is a reference-preserving extraction, not a redesign. Enlarge it cleanly to a useful production resolution, object filling about 85% of the output, with comfortable transparent margin, centered and fully visible. Remove all parchment, map, shadows beyond a restrained contact shadow, labels, text, and other UI. Do not substitute a glowing cyan orb or polished yellow-gold object. The output must have genuine transparent alpha around the object, with no rectangular backdrop, no baked checkerboard, and no added frame.

## Mural extraction prompt

Use case: background-extraction. Asset type: transparent production sidebar mural. Isolate and reconstruct ONLY the existing ancient Nika sun-warrior engraving in the lower left navy sidebar of this exact screenshot (approximately x24-373,y553-886). Preserve its same pose, laughing profile, raised sword angled upper-left, curling cloud hair, dotted round shield to the right, bent dancing legs, and surrounding restrained weathered curls. Preserve the proportions and thin, eroded bronze-ochre ink from this reference, not a new mascot or scene. Complete small peripheral curls naturally so no silhouette is cut by a rectangular image edge. It should look like a single worn ink stamp over the application's own navy background. Only the faded bronze ink strokes should be visible; navy/dark gaps between strokes and surrounding space must be genuinely transparent alpha. No dark-blue painted rectangle, no parchment, no text, no border, no controls, no gradients behind the subject. Leave about 8% transparent breathing room on every side. Keep all figure and linework inside the image.

## Final mural alpha repair prompt

Use case: background-extraction, precise alpha repair of an existing production asset. Keep exactly this dancing warrior's pose, sword, shield, facial expression, curling hair and surrounding curls. Change only the transparency and ink treatment. Remove ALL the brown/gold glow and dark painted background that currently surrounds and fills the gaps of this stamp. This is a FLAT one-color weathered stencil, not a lit object: muted ochre-bronze ink strokes only (#B59058), with worn speckling. Every negative space between arms, hair curls, shield dots, legs and clouds must be completely transparent alpha, including all dark fills. No shadows, no gradient, no halo, no vignetting, no navy/black/brown backing. Flat isolated bronze ink stencil on actual transparent alpha. No bounding rectangle. Complete figure fully visible, slightly generous transparent breathing room; use a tight portrait composition. Do not redraw or alter the warrior design.
