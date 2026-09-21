# Pirate asset prompts

Mode: built-in image generation. Controller assets reference the supplied `input_file_0.jpeg`. The four [navigator assets](navigator-asset-prompts.md) reference `Floating_clock_UI_design_20260921102153.jpeg`; their generated PNGs were copied unchanged into `StopwatchOverlay/Assets/Pirate/`. Layer clipping, live text and button glyphs are rendered by WPF.

## controller-background.png

Use case: precise-object-edit. Asset type: production WPF application decorative background plate. Edit the supplied 1200x896 pirate stopwatch reference into a clean empty background at the same composition and aspect ratio, high resolution. Keep the dark walnut wooden frame and grain, four riveted brass outer corners, header and menu planks, divider, dark wooden left sidebar with the faded straw-hat skull watermark, and right parchment map with worn edges, double border, faint coastlines, upper-left and lower-left compass roses, upper-right treasure X, and bottom-right coiled sea dragon. Retain the gold flourish under the title. Remove all text, digits, buttons and their frames, timer cards, separator swords, radio buttons, status dots, and action icons. Fill removed regions with the original wood or parchment. Keep calm empty central space for live controls. Full-bleed flat orthographic UI background; no perspective, external margins, or added objects.

## icons.png

Use case: background-extraction. Asset type: transparent PNG game UI icon atlas. Recreate decorative icons from the reference in a regular 4-column by 2-row grid on a transparent alpha background, one centered icon per cell with generous margins, no labels or frames. Top row: golden sun/lion ship figurehead with small ivory bones; ivory ram ship figurehead with brown curled horns; brass telescope with turquoise glass; tarnished silver anchor. Bottom row: crossed steel pirate swords; straw-hat skull and crossbones with red band; painted red X with diagonal brown paintbrush; brass navigation compass on leather/wood base. Match the ink-outlined, subtly pixel-edged painted inventory art. No flat emoji, modern 3D icons, or cast shadows beyond the icons. The final compass was replaced by the separate Log Pose asset below.

## brass-plate.png

Use case: background-extraction. One empty wide rectangular button face faithfully matching the reference Start/Reset brass-and-wood buttons. No words, letters, icons, characters, or symbols. Weathered brushed antique brass, golden ochre top lip, brown burnished lower edge, narrow engraved double-bevel border, slightly chipped corners, four small slotted round rivets, scratched patina, and horizontal grain warmth. Flat front-on GUI panel with near-rectangular corners and crisp dark outer outline. Genuine transparent alpha outside the rectangle. No perspective or filigree. The controller uses its center surface as a texture inside native, responsive riveted button templates.

## navigation-tools.png

Use case: stylized-concept. Transparent pirate UI atlas with two equal cells, no labels, frames, words, or extra objects. Match the reference painted pirate inventory art, dark ink outlines, warm brass, and subtle pixel edges. Left: recognizable Log Pose, a clear spherical glass dome on a round honey-brown wooden base, brown leather wrist strap, a single thin red magnetic needle floating diagonally inside, and turquoise glass reflections. Sphere and needle, no flat watch face, numbers, or compass letters. Right: small carpenter's hammer, worn steel rectangular head, brown wooden handle and gold ferrule, diagonal lower-left to upper-right like the reference New Timer hammer. Each centered with generous transparent margins.
