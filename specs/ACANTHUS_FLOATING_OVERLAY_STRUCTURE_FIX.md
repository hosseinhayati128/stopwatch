# Acanthus Floating Overlay Structural Fix

## Role

Use Codex with the connected Figma MCP tools to update the Acanthus floating overlay designs so they match the real product structure more closely.

This is a structural correction task, not a full redesign.

Keep the existing Acanthus visual language. Only fix the floating overlay structure and interaction pattern.

## Figma file

Use this file:

https://www.figma.com/design/q1GhB2cTVZiVWhbajEGT4K/StopWatch?node-id=35-2&t=3We7qYXfMFmyg18F-1

## Task goal

The floating clock should match the real app structure more closely.

The structure to match is:

1. The time is the main focal element
2. The project name sits under the time
3. The floating overlay itself is simple and compact
4. On mouse hover, the controller buttons appear
5. The hover controllers appear as a separate small control strip under the floating clock
6. Background transparency or opacity is controllable and should be represented clearly in the relevant overlay state
7. The overlay should still feel lightweight, readable, and practical

This is a structure fix, not a stylistic departure.

## Optional local reference images

If these local reference images are available, inspect them before editing:

- `design-references/overlay-structure-reference-1.png`
- `design-references/overlay-structure-reference-2.png`

Use them only to understand structure and behavior, not as a style template.

If they are not available, continue without stopping.

## Existing Acanthus reference

Inspect the current Acanthus floating overlay work and the approved Acanthus controller before making changes.

### Floating overlay source area

- Page: `13:11`, `08 - Floating Overlay States`
- Source section: `35:54`, `Floating Overlay States`

### Approved Acanthus controller

- Page: `13:6`, `03 - Controller and Timer Workspace`
- Section: `88:878`, `Theme Study / Acanthus`
- Approved controller frame: `88:879`

Use the approved Acanthus controller as the visual source of truth for palette, typography, borders, spacing, and ornament restraint.

## What to change

There are currently 2 new Acanthus floating overlay designs.

Update those designs so the structure matches the real product behavior.

You may:

- edit the 2 existing Acanthus overlay frames
- add up to 2 additional overlay frames only if clearly needed

Maximum final number of Acanthus overlay frames after this fix: 4

Do not create more than 4 final Acanthus overlay frames in total.

## Required structural behavior

### Base overlay card

The overlay card itself should contain only the essential timer information:

- large time value at the top
- project name directly underneath
- optional small state or mode indicator only if it fits cleanly
- no crowded layout
- no large extra descriptive blocks
- no unnecessarily wide layout

Preferred hierarchy:

- Time: dominant
- Project name: secondary
- Small metadata: tertiary if needed

### Hover controls

When hovered, show a separate control strip below the timer card.

The hover control strip should contain these actions:

- Close
- Start or Pause depending on state
- Reset

The controls should feel like a compact utility strip, visually related to the overlay but still distinct from it.

The controls should not permanently occupy space inside the overlay card.

### Transparency or opacity

The design should clearly show that the overlay background can have adjustable transparency.

Represent this in at least one overlay example by showing the overlay against a desktop-like sample background where opacity and readability can be judged.

Do not overcomplicate this. Make the transparency capability visually clear.

## Final required states

At minimum, ensure the final Acanthus overlay set clearly covers these two states:

1. `Overlay / Acanthus / 01 Active Running`
   - simple compact overlay
   - large time
   - project name under time
   - no hover controls visible

2. `Overlay / Acanthus / 02 Hover Controls`
   - same overlay structure
   - hover control strip visible below
   - Close, Start or Pause, and Reset buttons visible

If needed, you may add up to two more, such as:

3. `Overlay / Acanthus / 03 Transparent Preview`
   - demonstrates opacity or transparency behavior clearly

4. `Overlay / Acanthus / 04 Combined`
   - only if combined mode still needs a separate example
   - should still respect the simplified structural logic as much as possible

Only create extra states if they add clear value.

## Design rules

Keep the Acanthus theme consistent:

- same palette
- same serif, sans, and mono logic
- same restrained gold rules
- same deep olive and parchment family
- same subtle ornament approach
- same corner flourish restraint
- same light shadow behavior

The overlay must remain practical first.

Do not let decoration reduce readability.

### Overlay styling guidance

- use warm ivory or parchment-like surface
- maintain good contrast for timer digits
- preserve monospaced time values
- keep project name centered or carefully aligned beneath the time
- use a compact card size close to the real product behavior
- maintain clean spacing
- allow enough empty space around the digits
- keep ornament minimal, possibly only tiny corner details or a very subtle crest accent

### Hover controls styling guidance

- buttons should be compact and easy to click
- use icon-first or icon plus minimal styling
- keep the strip visually separate below the overlay
- match the theme but keep it functional
- do not turn the control strip into a decorative panel

## Constraints

Do not edit or restyle unrelated pages.

Do not modify shared component masters unless absolutely necessary.

Do not change the approved Acanthus controller.

Do not create a full redesign of overlays beyond this structural correction.

Do not create more than 4 final Acanthus overlay frames total.

Do not remove the Acanthus identity.

## Execution steps

1. Inspect existing Acanthus overlay frames
2. Inspect the approved Acanthus controller
3. Update the existing Acanthus overlay frames first
4. Add extra overlay frames only if clearly needed
5. Validate each final overlay with a screenshot
6. Check:
   - time is dominant
   - project name is under time
   - hover controls are separate and below
   - transparency is represented clearly
   - theme consistency is preserved
   - readability remains strong

## Completion response

When done, report:

1. which Acanthus overlay frames were edited
2. which new overlay frames were added, if any
3. final overlay frame names and node IDs
4. total final Acanthus overlay count
5. confirmation that the structure now matches:
   - time on top
   - project name below
   - hover controls below the overlay
   - transparency represented
6. confirmation that the approved Acanthus controller and unrelated content were not changed
