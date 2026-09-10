# Acanthus Theme Expansion for the Remaining Stopwatch Overlay App

## Role

Act as a senior product designer and Figma systems designer. Use Codex with the connected Figma MCP tools to extend the approved Acanthus visual language across the remaining Stopwatch Overlay application.

The approved Acanthus controller is already complete. Treat it as the visual source of truth. This task is not a broad redesign and it is not a request for every possible state. Create only the carefully selected screens listed below.

## Target Figma file

Use this exact Figma Design file:

https://www.figma.com/design/q1GhB2cTVZiVWhbajEGT4K/StopWatch?node-id=35-2&t=3We7qYXfMFmyg18F-1

## Optional local mood reference

Before working in Figma, inspect this local image when it is available:

`design-references/acanthus-moodboard.webp`

Use it only as a conceptual mood and ornament reference. Do not copy its invitation, book-cover, or historical print layouts. The approved Figma controller is the primary product-design reference.

If the image is not available, continue without stopping and rely on the approved Acanthus controller.

## Approved Acanthus source of truth

Use these existing Figma nodes as the authoritative Acanthus reference:

- Page: `13:6`, `03 - Controller and Timer Workspace`
- Section: `88:878`, `Theme Study / Acanthus`
- Approved frame: `88:879`, `Controller / Acanthus POC / Running Stopwatch`

Inspect the approved frame visually and programmatically before creating anything. Extract and reuse its actual visual properties rather than approximating them.

The approved controller currently establishes this language:

### Core colors

- canvas parchment: `#F3EFE5`
- warm ivory surface: `#FBF8F1`
- soft stone surface: `#E5DDCF`
- muted sage surface: `#D5DDCF`
- deep olive: `#445140`
- charcoal ink: `#2C2924`
- muted text: `#706A61`
- antique gold: `#B08A4D`
- soft gold rule: `#D1BC8D`
- botanical success green: approximately `#4F755A`
- restrained burgundy: `#8A3E45`

### Typography

- display and section headings: `Cormorant Garamond`, usually SemiBold
- interface labels and body copy: `Inter`
- timer values, durations, and keyboard shortcuts: `Cascadia Mono`

Use the exact available font style names in Figma. In particular, Inter uses `Semi Bold`.

### Geometry and depth

- restrained 4 px or 8 px spacing rhythm
- common control radii around 6 px
- common card radii around 8 px
- selective larger radii around 10 to 12 px
- fine olive and antique-gold borders
- subtle shadow only, approximately 0 px x, 3 px y, 12 px blur, 7 percent deep olive
- no heavy bevels, metallic gradients, or dense wallpaper

### Ornament grammar

Continue the same restrained ornament system:

- a compact acanthus crest in major application headers
- thin gold rules
- mirrored corner flourishes only on focal or framing surfaces
- a small botanical divider in selected content areas
- no decoration behind essential text
- no ornate border on every card
- no photographic foliage
- no emojis or clip art
- no fantasy, medieval, or invitation-card styling

## Protection rules

Do not edit, move, rename, delete, detach, or restyle any existing Figma content.

Do not modify:

- the approved Acanthus controller frame `88:879`
- the Acanthus section `88:878`
- existing Professional v2 screens
- existing shared component masters
- existing variable collections
- existing text, color, effect, or grid styles

All new work must be created in new Acanthus sections on the relevant existing pages.

When a reference screen contains instances from `Stopwatch Professional v2`, use it only to understand structure and content. Create new local Acanthus layers or detached local copies. Never alter the shared master component.

## Hard screen budget

The final file must contain exactly the following new design frames for this task:

| Part | New final designs |
| --- | ---: |
| Controller and Timer Workspace | 0 |
| Settings and Overlay Inspector | 3 |
| Project Records | 1 |
| Analytics Dashboard | 2 |
| Dialogs | 2 |
| Floating Overlay States | 2 |
| **Total** | **10** |

Do not create any additional final screens.

Temporary construction frames are allowed only during work and must be deleted before completion. Component masters and ornament building blocks do not count as screens, but keep them minimal and do not create a component showcase or documentation board.

Do not create:

- another controller screen
- dark or light variants
- narrow-width variants
- empty, loading, error, or stale states
- separate hover, focus, pressed, or disabled state boards
- extra modal states
- mobile layouts
- prototype-flow pages
- design-system documentation
- code implementation

If a critical controller inconsistency is discovered, report it at the end. Do not create or modify a controller frame without explicit approval.

## Placement and section rules

Create one new section on each relevant source page. Place every new section in clear empty space, at least 160 px from existing sections. Scan current page bounds before positioning.

Use these exact section names:

1. `Theme Study / Acanthus / Settings and Overlay Inspector`
2. `Theme Study / Acanthus / Project Records`
3. `Theme Study / Acanthus / Analytics Dashboard`
4. `Theme Study / Acanthus / Dialogs`
5. `Theme Study / Acanthus / Floating Overlay States`

Keep frames arranged from left to right in the order specified below, with approximately 80 px between frames and at least 40 px internal section padding.

## Required designs

# Part 1: Settings and Overlay Inspector

## Source structure

Use:

- Page: `13:7`, `04 - Settings and Overlay Inspector`
- Source section: `35:18`, `Settings and Overlay Inspector`
- Structural references:
  - `35:19`, `Inspector / 01 Overlay and Position`
  - `35:20`, `Inspector / 02 Timer Appearance`
  - `35:21`, `Inspector / 03 Backgrounds`
  - `35:23`, `Inspector / 05 Light Ring On`
  - `35:24`, `Inspector / 06 Behavior and Application`

Create exactly three frames, each `1040 x 720 px`.

## Frame 1

Name:

`Settings / Acanthus / 01 Overlay and Position`

Preserve the proven settings shell:

- 52 px application header
- left live-preview workspace around 420 px wide
- right settings inspector around 620 px wide
- compact settings navigation
- clear content pane

Show:

- live Acanthus overlay preview
- display selector
- six-position picker
- custom-position action
- Show overlay switch
- Click-through switch
- Hide from capture switch
- short dependency/help text

Use a light parchment and sage treatment that clearly separates preview, navigation, and content. The preview should resemble a desktop stage, not a decorative poster.

Use ornament sparingly:

- header crest
- one fine gold divider near the section title
- small mirrored corner details on the overlay preview only

## Frame 2

Name:

`Settings / Acanthus / 02 Appearance and Background`

Use the same shell and keep the live preview visible.

Combine the most important Appearance and Background controls into one coherent, vertically scrollable inspector state. The frame should show the upper and most useful portion of that content without overcrowding.

Include:

- font selector
- time format selector
- timer size slider
- outline thickness slider
- text color and outline color controls
- clock opacity
- two to four background thumbnails
- background strength slider
- add and remove background actions
- a short note about imported images and persistence

The Acanthus preview should respond visually through typography, border, and parchment treatment.

Do not use the moodboard image as the application background. Background thumbnails may include subtle abstract parchment, sage, line-art foliage, or plain surfaces, but must remain quiet enough for timer readability.

## Frame 3

Name:

`Settings / Acanthus / 03 Light Ring, Behavior and Application`

Use the same shell.

Show a live preview with the light ring enabled, then organize the content into three compact groups:

### Light ring

- Enable
- Brightness
- Width
- Hide light ring from capture

### Timer behavior

- Auto-start on show
- REC indicator
- Blink colon
- Click-through dependency note when relevant

### Application

- Start with Windows
- theme choice showing Acanthus as selected
- short notification-area behavior note

A gentle botanical halo may inform the light-ring preview, but it must still look like a practical screen-light effect rather than a decorative wreath.

# Part 2: Project Records

## Source structure

Use:

- Page: `13:9`, `06 - Project Records`
- Source section: `35:36`, `Project Records`
- Primary reference: `35:37`, `Records / 01 Populated`
- Locked-state reference: `35:39`, `Records / 03 Active Locked`

Create exactly one frame, `1120 x 720 px`.

Name:

`Project Records / Acanthus / Populated`

Include:

- Acanthus application header
- Project Records title and update status
- project filter
- Refresh action
- Add record primary action
- three summary cards
- records table
- pagination and saved status
- four representative records
- at least one active running record that is visibly locked from editing
- completed records with edit and delete actions

Use the source data and wording from the reference frames. Preserve the practical table hierarchy.

Acanthus treatment:

- serif page title and summary-card values where appropriate
- sans-serif table labels and row text
- monospaced dates, times, and durations where useful
- thin gold table rules
- deep olive active-row marker
- a small lock icon plus text for the running record
- no leaf motifs inside each row
- at most one subtle botanical divider near the page title or summary region

The table must remain the dominant information surface. Ornament must never reduce scanability.

# Part 3: Analytics Dashboard

## Source structure

Use:

- Page: `13:8`, `05 - Analytics Dashboard`
- Source section: `35:27`, `Analytics Dashboard`
- Main reference: `35:28`, `Analytics / 01 Dark Populated`
- Seven-day reference: `35:31`, `Analytics / 04 7 Days`
- Thirty-day reference: `35:32`, `Analytics / 05 30 Days`

Create exactly two frames, each `1120 x 800 px`.

## Frame 1

Name:

`Analytics / Acanthus / 01 Seven Days`

Show:

- Analytics title and refreshed timestamp
- project filter
- Today, 7d, 30d, and All range selector, with 7d selected
- Refresh and Records actions
- four KPI cards
- Time by project
- Daily totals
- Daily timeline with overlap lanes
- Session details
- explanatory footer note

Reuse representative source values such as:

- Total time: 42h 18m
- Sessions: 28
- Average session: 1h 31m
- Longest: 3h 42m

The data visualization must use a muted, accessible botanical and mineral palette. Preserve clear project differentiation and pair colors with labels, values, or markers.

Do not turn bars, timeline segments, or charts into leaves or vines. Use classical framing around charts, not decorative data marks.

## Frame 2

Name:

`Analytics / Acanthus / 02 Thirty Days`

Show:

- the same consistent dashboard shell
- 30d selected
- four KPI cards
- Time by project
- weekly or grouped daily totals
- a calendar-style activity heatmap
- concise session or activity summary

This second frame must demonstrate how the Acanthus system handles a denser, longer-range analytical view without becoming visually heavy.

Use antique gold mainly for rules, selected controls, and one data series. Use deep olive and moss for primary series. Reserve burgundy for warnings or destructive meaning, not ordinary chart decoration.

Add only one subtle ornamental element in the dashboard content area, such as a thin botanical divider beneath the page title. Charts and cards must remain clean.

# Part 4: Dialogs

## Source structure

Use:

- Page: `13:10`, `07 - Dialogs and Shortcuts`
- Source section: `35:43`, `Dialogs and Shortcuts`
- Project references:
  - `35:44`, `Dialogs / 01 Choose Project`
  - `35:45`, `Dialogs / 02 Create Project Inline`
- Record reference:
  - `35:47`, `Dialogs / 04 Add Record`

Create exactly two frames, each `640 x 640 px`.

## Frame 1

Name:

`Dialog / Acanthus / 01 Choose or Create Project`

Create one coherent project-chooser state with inline project creation visible. Include:

- dialog title
- short explanation
- project selector
- create-project control
- new-project-name field
- existing project choices
- No project option and reporting explanation
- Cancel
- Use project or Create timer primary action
- close action
- clear keyboard and focus hierarchy

Use the structure of the existing chooser, but avoid making the modal resemble an invitation card. A crest or tiny centered leaf mark may appear in the title region, while the body remains modern and functional.

## Frame 2

Name:

`Dialog / Acanthus / 02 Add Project Record`

Create the valid, populated Add Project Record state. Include:

- project selector
- optional new-project action
- Start date and 24-hour time
- End date and 24-hour time
- duration preview
- Cancel
- Add record primary action
- close action
- concise local-time guidance

Use a two-column field layout where it improves scanning. Keep input boundaries clear. Use monospaced values for time and duration.

Do not create separate edit, validation, delete-confirmation, or shortcut-editor frames in this task. Their future styling should be derivable from these two established dialog patterns.

# Part 5: Floating Overlay States

## Source structure

Use:

- Page: `13:11`, `08 - Floating Overlay States`
- Source section: `35:54`, `Floating Overlay States`
- Default reference: `35:55`, `Overlay / 01 Default Named`
- Hover reference: `35:59`, `Overlay / 05 Hover Controls`
- Combined reference: `35:65`, `Overlay / 11 Combined`

Create exactly two context frames, each `680 x 420 px`.

Each context frame should show the overlay against a simple neutral desktop sample so opacity, edges, and readability can be judged.

## Frame 1

Name:

`Overlay / Acanthus / 01 Active Running`

Inside the context frame, create one named active running overlay close to `360 x 142 px`.

Include:

- project name
- running state
- large stopwatch value
- timer mode
- keyboard shortcut hint
- active-state indication

Use:

- warm ivory surface with enough opacity for busy backgrounds
- charcoal timer digits
- deep olive active treatment
- fine antique-gold edge or inner rule
- subtle shadow
- only tiny corner flourishes with a large safe area around text

The overlay must remain clearly readable at a glance. Do not shrink the timer to make room for decoration.

## Frame 2

Name:

`Overlay / Acanthus / 02 Combined with Hover Controls`

Inside the context frame, create a combined overlay close to `360 x 174 px`, showing several timer rows and a clear active timer. Also show the hover toolbar attached below or near the overlay.

Include:

- active timer
- additional running or paused timers
- status markers
- monospaced values
- Close, Pause or Resume, and Reset controls in the hover toolbar
- clear indication that combined mode affects presentation only

Use one small botanical divider between the active row and the remaining timer list, if space permits. Do not place full corner ornaments around every row.

The hover toolbar must feel like part of the same Acanthus product system while remaining compact, fast to understand, and easy to click.

# Shared design rules

## Product consistency

Every new frame must clearly belong to the same application as approved frame `88:879`.

Reuse the same:

- header height and identity pattern
- title typography
- body typography
- monospaced numeric styling
- control radius
- button hierarchy
- border weights
- surface palette
- ornament stroke style
- status color meanings
- spacing rhythm

Do not independently reinterpret Acanthus on each page.

## Usability and accessibility

- body text should target at least 4.5:1 contrast
- large text and meaningful UI boundaries must remain clearly visible
- no status may rely on color alone
- controls should generally be at least 40 to 44 px high
- primary actions must be visually obvious
- tables and charts must remain easy to scan
- essential controls must never sit under decorative paths
- ornament layers must not capture interaction
- timer digits must remain tabular and immediately readable
- keep labels short and avoid unnecessary uppercase
- preserve clear keyboard focus hierarchy in dialogs and settings

## Ornament density by surface

Use more ornament only on:

- app header identity
- main preview or hero frame
- dialog shell title area
- floating overlay outer shell

Use very little or no ornament on:

- tables
- chart plotting areas
- input fields
- switches
- pagination
- repeated list rows
- action toolbars

## Images and texture

Do not import the moodboard into the Figma screens.

Do not use raster images as the core ornament system.

Create acanthus motifs as editable vector paths. Reuse or carefully adapt the existing crest, corner, and divider geometry from the approved controller without modifying the original nodes.

A faint paper feel may be created with restrained gradients or vector noise only when it does not harm performance or readability. Flat surfaces are acceptable and preferred over fake texture.

# Figma MCP execution requirements

Before the first write action:

1. Load and follow the relevant Figma MCP instructions or skills, including `figma-use` and `figma-generate-design` when available.
2. Inspect the approved Acanthus controller with screenshot and node-property reads.
3. Inspect the listed source frames for structure and content.
4. Confirm internally that the final screen budget is 10 and that no controller frame will be created.

Work sequentially and incrementally.

Recommended order:

1. Create minimal reusable Acanthus building blocks needed by several screens
2. Settings frame 1
3. Settings frame 2
4. Settings frame 3
5. Project Records
6. Analytics frame 1
7. Analytics frame 2
8. Dialog frame 1
9. Dialog frame 2
10. Overlay frame 1
11. Overlay frame 2
12. Final cross-screen consistency audit

Figma API rules:

- switch to at most one page per `use_figma` call
- remember that page context resets between calls
- use `await figma.setCurrentPageAsync(page)`
- return every created or mutated node ID
- load the exact current font before every text mutation
- use auto-layout for structurally related elements
- do not leave placeholder shimmer or temporary frames
- do not guess node IDs
- validate each completed frame with metadata and a screenshot before moving on
- on a tool error, stop, diagnose, and correct the call instead of blindly retrying
- keep a resumable local state file named `design-state-stopwatch-acanthus-expansion.json`

Do not modify local application code. The only intended deliverables are the Figma designs and the small local state file.

# Quality-control passes

For each final frame:

1. Capture a screenshot
2. Check text clipping and truncation
3. Check spacing and alignment
4. Check contrast
5. Check primary-action hierarchy
6. Check that ornaments do not intersect labels or controls
7. Compare palette, typography, and border language with frame `88:879`
8. Refine at least once when a visible problem exists

After all screens are complete, perform one cross-screen audit for:

- consistent header identity
- consistent title styles
- consistent control sizing
- consistent surface colors
- consistent button roles
- consistent chart and project colors
- consistent ornament stroke and density
- exactly 10 new final frames
- no changed existing nodes

# Completion checklist

The task is complete only when:

- the approved controller remains unchanged
- exactly three Settings and Overlay Inspector frames exist
- exactly one Project Records frame exists
- exactly two Analytics Dashboard frames exist
- exactly two Dialog frames exist
- exactly two Floating Overlay frames exist
- no additional Controller frame exists
- all 10 final frames use the approved Acanthus language
- all screens have been screenshot-validated
- temporary construction frames are removed
- existing Professional v2 and Acanthus content is unchanged

# Completion response

When finished, report:

1. each new section name and node ID
2. each final frame name and node ID
3. the final count by app part
4. any font substitution
5. any unresolved visual or structural concern
6. confirmation that frame `88:879` and all pre-existing content were left unchanged

Then stop. Do not create more screens until explicitly requested.
