# Stopwatch overlay design

Date: 2026-10-31. Status: visual choices approved; consolidated specification and implementation approach prepared for review. No production implementation is included in this design session.

## Intent and evidence

Build the floating stopwatch as independently selectable visual layers, consistent with the One Piece / Nika design work. The user retained collections rather than choosing one exclusive skin. Preserve every retained choice and make the approved combination the initial appearance of the new profile.

The source of truth for choices, original option numbers, prompts and revision history is [design-exploration.json](../../../design/one-piece-v2/stopwatch-overlay/design-exploration.json). Images are appearance references, not production assets or measurements of alpha compositing. The user's latest “yes good continue” accepts the recommended default combination and continuation into consolidation and planning.

This specification distinguishes approved product decisions from engineering choices made to build them. It does not claim that font substitutions, migration details or native rendering have already been reviewed by the user.

## Goals, boundaries and anti-goals

- Retain 12 backgrounds, 16 frames, 10 typography profiles, 3 artwork treatments and 12 hover-control styles as independent selectors.
- Reproduce Project Markers revision 2, the ACTIVE tab, 70% background opacity and 40% unselected opacity.
- Add the approved Style / Transparency inspector with immediate application and a shared live renderer for previews and actual overlays.
- Preserve all four timing modes, timer values, combined/herd and separate overlays, close/pause/reset semantics, drag, shortcuts, click-through, capture hiding, topmost and no-activate behavior.
- Target `StopwatchOverlay.Desktop` (Avalonia), with settings and pure policies in Core. Do not implement a second renderer in the legacy WPF application.
- Do not redesign the controller, Todo overlay, unrelated themes or integrations. Do not expose hidden Edit or Separate/Merge hover actions.
- Do not flatten a concept board into a clock image, bake the time or project name into artwork, merge independent selections into fixed skins, add unselected candidates, add a motif picker, or restore the removed underline.
- Do not add blinking status, hover brightening, automatic frame/control pairing, a Save/Apply step, or new timer behavior.

## Approved default

| Layer | Label | Persisted ID |
| --- | --- | --- |
| Background | Navigator Parchment | `navigator-parchment` |
| Frame | Engraved Filigree | `engraved-filigree` |
| Typography | Navigator Bold | `navigator-bold` |
| Artwork | Clean | `clean` |
| Hover controls | Filigree Keys | `filigree-keys` |
| State indicators | Project Markers, revision 2 | `project-markers` |

Background opacity defaults to **70%**. Unselected whole-clock opacity defaults to **40%**; selected is **100%**. The sample text size 48 is the existing code default, not a new user selection; preserve saved text size and its 16–120 range. Selecting a different frame must not change another layer.

## Retained collections

Labels and IDs below are exhaustive. Historical option numbers belong to their own boards: control #14 is Filigree Keys, while original frame #14 is Open Corners.

### Backgrounds — 12

| ID | Label |
| --- | --- |
| navigator-parchment | Navigator Parchment |
| amber-vellum | Amber Vellum |
| midnight-chart | Midnight Chart |
| quiet-atlas | Quiet Atlas |
| ships-timber | Ship's Timber |
| sailcloth | Sailcloth |
| captains-leather | Captain's Leather |
| aged-bronze | Aged Bronze |
| tideglass | Tideglass |
| wano-waves | Wano Waves |
| celestial-navigation | Celestial Navigation |
| sky-island | Sky Island |

Use `background-options-01-04-v1.png` and `background-options-05-12-v1.png` in the design directory. All twelve were kept; “all ten” was resolved in chat as keeping the complete shown collection.

### Frames — 16

| ID | Label |
| --- | --- |
| classic-brass | Classic Brass |
| navy-bevel | Navy Bevel |
| walnut-rim | Walnut Rim |
| pixel-voyage | Pixel Voyage |
| instrument-case | Instrument Case |
| slim-capsule | Slim Capsule |
| rope-binding | Rope Binding |
| chart-scroll | Chart Scroll |
| admiralty | Admiralty |
| bound-logbook | Bound Logbook |
| cloud-crest | Cloud Crest |
| tide-curve | Tide Curve |
| engraved-filigree | Engraved Filigree |
| open-corners | Open Corners |
| vine-filigree | Vine Filigree |
| royal-cartouche | Royal Cartouche |

The first fourteen were all retained. The later pairing selection adds Vine Filigree and Royal Cartouche without removing earlier frames. Sources: `shape-frame-options-01-06-v1.png`, `shape-frame-options-07-14-v1.png`, and options 15–16 of `filigree-frame-controls-15-20-v1.png`. Options 17–20 remain history only.

### Typography — 10

| ID | Label | Required visual character |
| --- | --- | --- |
| navigator-bold | Navigator Bold | Condensed bold sans-serif digits, classic bold serif name |
| captains-serif | Captain's Serif | Substantial high-contrast Roman serif digits, refined serif name |
| typewriter-log | Typewriter Log | Legible monospaced digits/name; no horizontal crossbar through zero |
| instrument-digital | Instrument Digital | Live seven-segment numerals, smaller condensed sans-serif name |
| pixel-voyage | Pixel Voyage | Square-pixel digits and compact blocky name |
| modern-deck | Modern Deck | Broad geometric sans-serif numerals, simple sans-serif name |
| art-deco | Art Deco | Tall narrow geometric numerals, fine inline detailing, matching name |
| rounded-deck | Rounded Deck | Soft rounded numerals, open counters, rounded name |
| chart-plotter | Chart Plotter | Narrow light-to-medium monospaced technical numerals and name |
| compass-outline | Compass Outline | Hollow geometric numerals with clean navy contours, single-stroke name |

Sources: `typography-options-01-08-v2.png` (v1 is superseded) and `typography-options-09-16-v1.png`. Real font mappings are engineering work, not established by image generation. Time and names must remain live text; digital/pixel/outline timer numerals may use live vector glyphs. Preserve accessible text even when painting custom glyphs. Numeric advance must not jitter each tick; support the existing time strings without changing their format. Keep letters and numeral zero unambiguous.

### Artwork — 3

`clean` (Clean), `corner-stamp` (Corner Stamp), `edge-engraving` (Edge Engraving), from `artwork-options-01-08-v1.png`, options 1, 5 and 8. Clean contains no emblem. Stamp and engraving remain subdued, monochrome and outside the text safe area.

Reuse the original Luffy emblem from `StopwatchOverlay.Desktop/Assets/Pirate/icons.png`, the second cell in the second row of the 4×2 atlas (normalized source rectangle .25, .5, .25, .5). Do not substitute an AI-generated approximation of this emblem. This design does not request an additional emblem collection.

### Hover controls — 12, with reference frame pairings

| Control ID | Label | Reference frame ID |
| --- | --- | --- |
| classic-brass | Classic Brass | classic-brass |
| compass-dials | Compass Dials | instrument-case |
| floating-buttons | Floating Buttons | slim-capsule |
| pixel-controls | Pixel Controls | pixel-voyage |
| bronze-keys | Bronze Keys | navy-bevel |
| quiet-glyphs | Quiet Glyphs | open-corners |
| wooden-deck | Wooden Deck | walnut-rim |
| porcelain-keys | Porcelain Keys | cloud-crest |
| glass-dock | Glass Dock | tide-curve |
| filigree-keys | Filigree Keys | engraved-filigree |
| vine-filigree | Vine Filigree | vine-filigree |
| royal-cartouche | Royal Cartouche | royal-cartouche |

These pairings explain the approved boards; they are not selection constraints. Sources: `hover-controls-matched-frames-01-06-v1.png`, `hover-controls-matched-frames-07-12-v1.png`, `hover-controls-matched-frames-13-14-v1.png`, and `filigree-frame-controls-15-20-v1.png`. Only retained IDs enter selectors.

Action order stays **Close, Pause/Resume, Reset**. Existing handlers and confirmation behavior remain authoritative. The toolbar appears below the clock without changing its layout; crossing from the clock to the toolbar must keep it open. Keep the existing 200 ms departure delay and suppress it entirely during click-through. Preserve real Buttons, tooltips, disabled states and accessible names. Decorations are not hit-test targets.

## State and layout

Reference: `state-indicator-05-project-markers-v2.png`.

- Running: small teal triangle immediately before the project name.
- Paused: small amber pause bars in the same fixed slot.
- Active selection: small navy-and-gold ACTIVE tab at the top-center frame edge, independently of running/paused.
- Inactive clocks have no ACTIVE tab. There is no underline in any state.
- Reserve the tab's geometry so selecting, pausing or starting never shifts the clock or its anchor. Reserve a common glyph slot for the project marker.
- Keep time and project-name rows readable in every frame. Constrain long names to the clock's available name area, use ellipsis without changing the stored name, and expose the full name through accessibility/tooltip. An empty name leaves the marker's row slot intact.

Engineering interpretation for existing modes: display the running cue for real-time clock mode, whose pause action remains disabled. For pausable modes, follow `session.IsRunning`; stopped/reset timers use the pause cue at zero without introducing a new visual state. Keep REC as its existing independent indicator if enabled. These rules change presentation only.

## Transparency and compositing

Background/map layers always follow the Background slider, range 0–100. The Unselected timer slider has range 10–100. Selected whole-clock opacity is 1; unselected uses the configured percentage divided by 100. Selection, not running status, controls dimming for both separate clocks and whichever session the herd overlay represents.

Logical keep-opaque groups: **Frame**, **Time text**, **Project name**, **State markers**, **Hover controls**, **Artwork**. First five default checked. Artwork is disabled for Clean; its stored preference survives switching to Clean and back. Engineering default for the non-Clean artwork checkbox is unchecked, matching the displayed disabled checkbox and preserving a subdued treatment.

For each layer, `effective alpha = intrinsic pixel alpha × layer multiplier × selection multiplier`. Layer multiplier is 1 when checked, Background/100 when unchecked; background/map always use Background/100. Keep-opaque does not make transparent pixels solid or remove the intrinsic muted artwork treatment. At defaults, unselected background is .70 × .40 = **.28**, and an opaque unselected frame/text/marker is **.40**.

State markers include the run/pause glyph and ACTIVE tab. Hover controls include the whole toolbar surface, ornaments, buttons and glyphs. A native Popup must receive selection dimming exactly once, even when it is a separate render root. Do not assume Window.Opacity propagates into it, and do not multiply selection dimming twice. Hover does not brighten a dimmed timer. Use native compositing checks; the concept image does not establish numeric alpha correctness.

## Settings experience

References: `appearance-settings-style-v1.png` and `appearance-settings-transparency-v1.png`.

Use the existing Settings and Overlay Inspector host: live preview left, navigation middle, scrolling inspector right; 1060×740 initial and 840×600 minimum. Add **Stopwatch appearance** adjacent to Todo appearance, retaining all unrelated categories and settings.

**Style:** five independent thumbnail-assisted selectors, text-size slider using existing 16–120 range, and a read-only summary of the approved state indicators. The counts are 12 / 16 / 10 / 3 / 12. No selectors for rejected candidates or alternate state styles.

**Transparency:** Background and Unselected timer sliders, then the six logical keep-opaque checkboxes. Show their percentages and the rule that unselected dimming applies after layer opacity.

**Preview:** selected and unselected sample clocks, sample time `00:44:27.0`, project `Illustration`; selected sample shows its toolbar. Running/Paused and Light/Dark backdrop switches are preview-only. They must not start/pause/select a real timer, modify real desktop themes, or persist synthetic runtime state. Keep the existing explicit Show/hide active overlay action separate. The preview uses the same renderer and toolbar styles as the live window.

Controls apply immediately through existing settings change/persistence flow. Done closes the window. Guard initialization and preview updates against spurious writes. The right inspector scrolls vertically; preview stays visible. At minimum size, let columns adapt and preview samples fit their column without clipping or horizontal overflow. Verify actual native dropdowns and sliders at multiple display scales.

## Engineering approach and compatibility

Chosen approach: a dedicated versioned stopwatch appearance profile, a scoped Nika overlay route, and a composable renderer behind the existing overlay window. Alternatives considered: extending the existing Navigator flags would conflate old theme behavior with new logical groups; replacing the entire window would unnecessarily risk timer input and native window behavior.

1. Add `StopwatchAppearanceSettings` to Core with the five independent IDs, 70/40 opacity defaults and six logical opaque flags. One shared appearance profile applies to all stopwatch windows; there is no per-timer style editor. State style is fixed, not a persisted choice. Keep legacy `NavigatorOpaqueParts` and its JSON values unchanged. Use finite-value validation and per-field fallback; unknown IDs fall back to the approved default for that layer.
2. Add an explicit Nika overlay option in `OverlayThemeCatalog`. Follow Application Theme resolves to the new appearance for Nika; explicit legacy One Piece/Pirate remains on its existing renderer. Selecting Nika independently works with other application themes. Returning to another overlay theme restores its existing appearance. This is the proposed integration choice, not a separately requested application theme change.
3. Fresh new profiles use 70/40. When loading an older saved JSON document with no stopwatch appearance object, copy any explicitly present finite legacy `BackgroundOpacity` and `InactiveSeparatedOverlayOpacity` values into the new profile once, clamping to its ranges. Missing fields use 70/40; an existing profile wins over legacy fields. Preserve the legacy fields themselves. Saved text size, unrelated appearance, Todo, timers and integrations remain unchanged. Do not modify live settings as part of this design task.
4. The new profile's typography is authoritative for Nika; existing font-family overrides continue to control legacy themes and are not erased. Retain shared text size and explicit text-color choice for Nika. Existing border customization stays on legacy themes; approved new frames own their decorative geometry. Keep old controls available where they apply, and prevent legacy opacity controls from appearing to edit an unrelated Nika value.
5. Delegate measure/render of new layers to focused components; keep `ThemedClockFace` as the shared live/preview entry point. Forward active/running/clock-mode presentation state. Keep native operations, timer actions and lifecycle inside the existing OverlayWindow/ControllerWindow integration.
6. Use cached assets and typefaces; do not load files, reconstruct entire visual trees or generate textures on a timer tick. Release native graphic resources according to existing control lifetime patterns. No new runtime rendering or network dependency is needed.

The exact font families and drawing geometry within each approved visual character, asset slicing, contrast tokens, safe insets and caching structure are builder-owned choices. They must pass the visual and behavior checks below; a rough generic frame or font is not an acceptable substitute for a retained design.

## Asset preparation and first implementation slice

Start with the default combination as a vertical slice: prepare its text-free background and transparent frame/control ornament, render real time/name text, markers, ACTIVE tab and exact opacity, and inspect it in the existing overlay. Then expand the same contracts to the full retained catalog and inspector. The default slice is a milestone, not permission to omit the remaining choices.

Store production assets under `StopwatchOverlay.Desktop/Assets/Stopwatch/`, with provenance and slicing data in `asset-manifest.json`. Keep the original concept boards and exact prompts in the design directory. Audit existing Pirate/Nika assets first and reuse those that actually match. Generate missing raster artwork with the image generation tool and the approved boards as references; inspect every new asset and transparent edge before using it. Do not crop live text, labels, desktop backgrounds or neighboring candidates into runtime layers.

Backgrounds tile or crop inside frame-specific masks. Frame corners/ornaments retain aspect ratio; rails stretch or tile independently. Include content/safe insets and minimum size in each frame definition. The same background must work with all sixteen frame shapes. Control ornament must not reduce button hit regions. Use deterministic scalable geometry for simple outlines, markers and glyphs; ornate frames require faithful artwork rather than arbitrary placeholder paths.

Bundle redistributable fonts with license files; verify their family names and runtime loading. Existing Barlow Condensed, Almendra, Cormorant Garamond, Cascadia Mono, Inter, Roboto Condensed, Rajdhani and Playfair Display are candidates, not an assertion that every selected style is already covered. New fonts, if needed, must have an identified source and redistribution license. Record exact mappings and compare live-rendered samples to the approved typography boards, especially zero, seven-segment, pixel, inline and outline profiles.

## Acceptance and verification

- Catalogs expose exactly the retained IDs and counts; every default exists and each selector persists independently. None of the four retained frames whose matching toolbar was rejected disappear.
- Fresh, legacy, partial, malformed and future-ID settings load deterministically; NaN/infinity cannot reach layout/compositing. Round trips preserve unrelated saved values and 0% background. Application/overlay theme choices remain independent.
- Default live appearance resembles the approved boards with real text and original artwork. Every retained asset loads; no clock contains board captions, baked digits or an accidental opaque rectangle.
- Exercise every background against every frame (192 preview combinations), each typography on light/dark backgrounds, all artwork treatments, and each toolbar with its reference frame plus the default frame. This is a visual matrix, not a claim to test every possible full product combination.
- Check selected-running, selected-paused, unselected-running, unselected-paused, reset-at-zero and real-time clock; switching states never moves the frame or adds an underline. Check REC separately. Hover action icon describes the action; marker describes current state.
- Verify .70/.40/.28 alpha numerically on controlled opaque sample pixels; check native popup dimming and keep-opaque behavior at 0%, 70%, 100% background and 10%, 40%, 100% unselected. Transparent ornament edges remain transparent.
- Check long and empty names, Persian/RTL names, leading zeros, hours beyond 99 and each time string the four existing modes produce; no clipped digits or tick-width jitter. Test text sizes 16/48/120 and native display scaling 100%/150%/200%.
- Keep clock-to-toolbar pointer crossing, 200 ms hide delay, click-through suppression, correct single action dispatch, disabled clock pause, right-click toggle, drag, hide/show and native topmost/no-activate/capture behavior. Combined and separate timers update the same appearance and selection semantics.
- Settings preview toggles cause zero timer commands and no real state changes. Check 840×600 and 1060×740; dropdown choices remain reachable and inspector does not overflow.
- Run Core-focused tests and actual Avalonia regression checks. WPF-oriented tests alone are not evidence of native Desktop UI correctness. Use isolated fixtures and save visual evidence outside live user data. Report any OS/display checks that could not be run.

## Completion boundary

This design phase delivers the updated exploration manifest, this consolidated specification and its implementation plan. Production assets, live font selection and native UI verification are planned work, not completed work. Implementation should begin with the default vertical slice and continue to all retained choices before the feature is considered complete.

Builder check, 2026-10-31: a fresh Avalonia engineering reviewer read the specification and reported “nothing important left.” This checks planning completeness, not production implementation or visual fidelity.
