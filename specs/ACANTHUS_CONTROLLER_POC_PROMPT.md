# Acanthus Controller Theme Proof of Concept

## Role

Act as a senior product designer and Figma systems designer. Use Codex with the connected Figma MCP tools to create one polished desktop controller panel as a visual proof of concept for a new **Acanthus** theme.

This is a theme exploration, not a full product redesign. The result must be functional, refined, editable, and suitable for direct visual comparison with the existing controller.

## Target Figma file

Use this exact Figma Design file:

https://www.figma.com/design/q1GhB2cTVZiVWhbajEGT4K/StopWatch?node-id=35-2&t=3We7qYXfMFmyg18F-1

Relevant nodes:

- Source section: `35:2`, named `Controller and Timer Workspace`
- Primary reference frame: `35:3`, named `Controller / 01 Default Dark`

Inspect the metadata and screenshot of the source frame before creating anything.

## Exact scope

Create **exactly one new controller panel**.

The required final frame is:

- Name: `Controller / Acanthus POC / Running Stopwatch`
- Size: `1040 × 720 px`
- State: running stopwatch
- Purpose: a single high-fidelity theme study for review

Create a new section named:

`Theme Study / Acanthus`

Place the section in clear empty space, preferably at least 160 px to the right of the existing `Controller and Timer Workspace` section.

The new section should contain only the final controller frame. A simple section title is enough.

Do not create:

- additional controller states
- dark and light variants
- settings screens
- analytics or records screens
- overlays
- mobile layouts
- component documentation
- a design-system page
- a color or typography board
- prototype flows
- implementation code
- additional theme concepts

After the single panel is complete and visually validated, stop and wait for feedback.

## Protect existing work

Do not edit, move, rename, delete, or restyle any existing Figma content.

Do not modify any shared component master, component set, variable collection, or style used by the existing `Stopwatch Professional v2` screens.

You may use frame `35:3` as the structural and content reference, but the Acanthus concept must be created as new local content. If duplicating the source frame helps, make all theme-specific changes only inside the duplicate. Detach instances locally when necessary rather than modifying shared masters.

## Content and information architecture

Keep the information architecture and sample content of frame `35:3` so the two designs can be compared fairly.

The new panel should include the same main regions:

1. Application header
2. Left timer rail
3. Active timer workspace
4. Project and status indicators
5. Stopwatch, Clock, Countdown, and Timecode mode selector
6. Large running timer display
7. Pause, Lap, Reset, and Show overlay controls
8. Recent laps panel
9. Keyboard shortcut hint

Reuse the existing labels, values, and sample data from frame `35:3`. Do not invent new features or change the product workflow.

Keep the overall two-column structure:

- left timer rail around 250 to 260 px
- main workspace filling the remaining width
- header around 52 to 56 px high
- comfortable desktop spacing
- controls at least 44 px high where practical

You may refine spacing, hierarchy, and surface treatment, but the result must still read immediately as the same controller.

## Theme concept: Acanthus

The Acanthus style comes from classical architectural ornament and the acanthus plant. It should communicate endurance, elegance, craft, and quiet prestige.

Translate the supplied visual references into a usable desktop product interface. The references are conceptual mood references, not layout templates. Do not make the interface look like a wedding invitation or a scanned book cover.

### Core visual qualities

- regal but restrained
- organic and symmetrical
- classical rather than medieval
- premium rather than theatrical
- lightly engraved rather than heavily illustrated
- warm, tactile, and calm
- decorative around the hierarchy, never behind essential content

### Suggested palette

Use a light parchment-based theme. These values are a starting point and may be adjusted slightly for accessibility and visual balance:

- parchment canvas: `#F3EFE5`
- warm ivory surface: `#FBF8F1`
- soft stone surface: `#E5DDCF`
- muted sage rail or secondary surface: `#D5DDCF`
- deep olive: `#445140`
- moss green: `#71806A`
- antique gold: `#B08A4D`
- soft gold line: `#D1BC8D`
- charcoal ink: `#2C2924`
- muted text: `#706A61`
- restrained burgundy for destructive or recording accents only: `#8A3E45`

Gold should function mainly as fine borders, rules, and ornamental linework. Do not use bright metallic gradients or large areas of saturated gold.

### Typography

Use a deliberate two-family or three-family system:

- classical serif for the product title, section headings, and a few display labels
- highly legible sans serif for body copy and control labels
- tabular monospaced type for timer values and lap values

Preferred options, subject to font availability:

- headings: `Cormorant Garamond`, `EB Garamond`, or `Georgia`
- interface text: `Inter` or `Segoe UI`
- timer numerals: `IBM Plex Mono`, `Cascadia Mono`, or `Consolas`

Verify exact available font names before applying them. Keep all text editable. Do not convert text to outlines.

Avoid script fonts, overly thin Didone fonts for small text, and decorative typography inside controls.

## Ornament strategy

The Acanthus character must be visible within a few seconds, but ornament must remain disciplined.

Use only a small set of coordinated vector motifs:

1. A compact mirrored acanthus crest or leaf medallion near the product title in the header
2. Fine acanthus corner flourishes or botanical brackets around the large timer hero
3. One subtle vine or leaf divider in the Recent laps panel or between major regions
4. Optionally, a very faint acanthus line-art watermark in the lower part of the timer rail, below 4 percent opacity

Ornaments should be:

- symmetrical or intentionally mirrored
- made from editable vector paths or clean SVG paths
- drawn with approximately 1 to 1.5 px strokes
- placed with generous clear space around labels and controls
- visually secondary to the timer and primary action
- consistent in curve style and stroke weight

Do not use:

- emojis
- clip-art leaves
- photographic foliage
- dense William Morris wallpaper across the entire interface
- ornate borders around every control
- decorative elements behind text
- excessive crowns, shields, ribbons, or monograms
- fake gold foil effects
- heavy bevels or skeuomorphic stone carving

## Panel design guidance

### Header

Create a calm ivory or muted sage header with a fine double rule or single antique-gold rule.

Use the acanthus crest as the app mark or beside the `Stopwatch Overlay` title. Keep top-level actions such as Analytics, Records, and Shortcuts visible but understated.

The header should feel like a refined museum catalogue or heritage stationery translated into modern software, not a literal invitation card.

### Timer rail

Use a subtly tinted sage or warm stone background.

The active timer card should have the clearest emphasis through a deep olive rule, antique-gold border, or a restrained inset treatment. Inactive cards should remain clearly readable without strong ornament.

Keep status dots and timer values functional. Use green, amber, and burgundy only where status communication requires them.

### Main workspace

Keep ample breathing room.

Use a refined segmented control for the four timer modes. The selected mode may use a soft sage fill, deep olive text, and a fine gold underline or border.

The timer hero should be the main visual focus. Use an ivory surface with a thin double-line frame or a single dark olive border plus subtle gold inner rule. Add the acanthus corner flourishes outside the text safe area.

The large timer value must remain highly legible, tabular, and dominant. Do not use decorative serif numerals for the main time display unless they remain perfectly aligned and readable.

### Actions

Use one clearly dominant primary action.

Suggested treatment:

- primary action: deep olive fill with warm ivory text
- secondary actions: ivory or stone fill with olive text and antique-gold or olive border
- destructive actions: restrained burgundy only when applicable
- checkbox: simple, modern, and readable

Buttons may have gently sculpted corners, but avoid exaggerated ornamental silhouettes.

### Recent laps

Use a light stone or ivory card with a classical serif heading, sans-serif explanatory copy, and monospaced lap values.

A thin botanical divider is welcome. Do not place a full floral illustration inside this card.

### Depth and texture

Use subtle elevation only. Prefer fine borders, tonal surface separation, and restrained shadows.

A very soft parchment gradient or faint paper grain may be simulated if it remains editable and does not reduce clarity. Do not rely on external raster textures.

## Accessibility and usability

The panel must remain a practical productivity interface.

Requirements:

- body text contrast should target at least 4.5:1
- large text and decorative borders should remain clearly distinguishable
- essential information must never rely on color alone
- controls should have clear boundaries and readable labels
- timer digits must be readable at a glance
- decorative elements must not interfere with hit areas
- keep a consistent spacing rhythm, preferably based on 4 px or 8 px increments
- maintain clear focus hierarchy and a single obvious primary action
- avoid overly small text and low-contrast gold body copy

## Figma construction requirements

Before any write action, load and follow the available Figma MCP instructions or skills relevant to editing and screen generation, including `figma-use` and `figma-generate-design` when available.

Work incrementally:

1. Inspect source section `35:2` and reference frame `35:3`
2. Create the new section and wrapper frame
3. Establish the background, header, and two-column structure
4. Build the timer rail
5. Build the active timer workspace
6. Add the restrained acanthus ornaments
7. Refine typography, spacing, and contrast
8. Validate using a screenshot
9. Correct clipping, overlap, weak hierarchy, or excessive decoration
10. Capture a final screenshot

Use auto-layout for structurally related content. Keep layers clearly named. Use editable native Figma layers and vectors. Do not rasterize the finished panel.

Do not create a full component library for this proof of concept. Small local reusable elements are acceptable only when they make construction cleaner, but they must remain scoped to this single exploration.

## Visual quality bar

The final panel should feel like a premium modern desktop tool designed for a museum curator, architect, conservator, editor, or researcher.

It should be recognizable as Acanthus without becoming:

- a wedding invitation
- a historical reproduction
- a fantasy game interface
- a luxury cosmetics package
- a floral scrapbook
- a generic beige dashboard with leaves added afterward

The theme should appear integrated into the structure through typography, rules, proportions, color, and carefully placed botanical geometry.

## Acceptance checklist

The task is complete only when all of the following are true:

- exactly one new 1040 × 720 controller frame exists
- the source frame and all existing content are unchanged
- the new frame uses the same product content and basic structure as frame `35:3`
- the Acanthus identity is immediately visible but restrained
- timer digits and controls remain highly readable
- there is one clear primary action
- no text is clipped or truncated unintentionally
- no ornament overlaps interactive content
- no new full-screen variants or supporting boards were created
- a final Figma screenshot has been reviewed and at least one refinement pass has been performed if needed

## Completion response

When finished:

1. Select the final Acanthus controller frame in Figma
2. Report its exact node ID and frame name
3. Provide a short summary of the palette, typography, and ornament choices
4. Mention any font substitution that was necessary
5. Stop

Do not continue to additional panels or screens until I explicitly approve this concept.
