# Implement the Final Acanthus Figma Design in the Local Stopwatch Overlay App

## Goal

Use Codex with the connected Figma MCP tools to read the finalized Acanthus designs from Figma and implement them in the current local WPF application.

The result must:

1. Add **Acanthus** as a new selectable and persisted theme.
2. Keep all existing themes:
   - Midnight
   - Daylight
   - Pixel Deck Night
   - Pixel Deck Day
3. Apply the **new Figma structure to the whole app**, regardless of theme.
4. Preserve all existing application behavior and stored user data.

The new layout is shared by every theme. Acanthus is only an additional visual theme.

Do not create duplicate Acanthus-only copies of each window.

## Figma file

Use this exact Figma file:

https://www.figma.com/design/q1GhB2cTVZiVWhbajEGT4K/StopWatch?node-id=35-2&t=3We7qYXfMFmyg18F-1

Use Figma as a read-only design source. Do not modify the Figma file.

## Technology

The local application is a native WPF application targeting .NET for Windows.

Implement the designs using the existing:

- C#
- XAML
- WPF resource dictionaries
- code-behind
- timer/session models
- settings persistence
- project-history persistence
- overlay windows
- multi-monitor logic

Do not:

- rewrite the application in another framework
- convert the whole project to MVVM
- add a third-party UI framework
- replace functional controls with screenshots
- paste Figma-generated React, Tailwind, HTML, or CSS into the app
- create one separate window implementation per theme

Small reusable WPF controls, styles, converters, and helper classes are allowed when they reduce duplication.

## Source-of-truth rule

Use:

- **Figma** as the source of truth for visual structure, hierarchy, spacing, typography, surfaces, borders, ornament, and responsive intent.
- **The existing local app** as the source of truth for behavior, state, persistence, validation, shortcuts, timers, records, analytics, overlays, and platform integration.

When visual structure and current implementation differ:

- adopt the new Figma structure
- preserve the existing feature behavior
- do not remove a feature just because its state is not shown in the Acanthus mockups

## Required Figma workflow

Before changing local code:

1. Load and follow the available `figma-design-to-code` guidance.
2. Inspect the current local project.
3. Run the baseline build and tests.
4. Read each final Acanthus frame with `get_design_context`.
5. Use screenshots to confirm visual hierarchy.
6. Download exact vector assets only when needed.
7. Build a mapping from Figma nodes to WPF windows and controls.
8. Then begin implementation.

Do not request one giant design-context response for the entire file. Read the main frames individually.

Treat generated web code as reference only and translate it into maintainable WPF.

## Final Acanthus designs to read

Resolve frames by **name first** and use the IDs below only as hints because IDs may change after edits.


## Strict Figma visual-source allowlist

The Figma file contains multiple visual directions. For this implementation, the only authoritative visual theme is **Acanthus**.

### Acanthus visual sources

Use `get_design_context` for visual implementation only on the following Acanthus frames:

- `88:879`  
  `Controller / Acanthus POC / Running Stopwatch`

- `105:368`  
  `Settings / Acanthus / 01 Overlay and Position`

- `110:830`  
  `Settings / Acanthus / 02 Appearance and Background`

- `114:367`  
  `Settings / Acanthus / 03 Light Ring, Behavior and Application`

- `105:2490`  
  `Project Records / Acanthus / Populated`

- `105:2494`  
  `Analytics / Acanthus / 01 Seven Days`

- `110:322`  
  `Analytics / Acanthus / 02 Thirty Days`

- `107:2577`  
  `Dialog / Acanthus / 01 Choose or Create Project`

- `107:2578`  
  `Dialog / Acanthus / 02 Add Project Record`

- The latest corrected frames inside section `107:2544`  
  `Theme Study / Acanthus / Floating Overlay States`

Resolve every target by name before using its ID because IDs may change after Figma edits.

### Non-Acanthus frames

Every frame outside the Acanthus sections is a behavior, state-coverage, or responsive-layout reference only.

Do not copy any of the following from non-Acanthus Figma frames:

- colors
- fills
- strokes
- shadows
- typography
- corner radii
- component styling
- decorative elements
- icon treatment
- visual hierarchy unique to Professional v2
- dark or light theme appearance

Non-Acanthus frames may only be used to understand:

- available application states
- controls that must exist
- interaction behavior
- data shown in each state
- responsive and minimum-width requirements
- validation and error states
- loading and empty states

The existing local WPF resource dictionaries are the source of truth for preserving Midnight, Daylight, Pixel Deck Night, and Pixel Deck Day. Do not use the Professional v2 Figma appearance to overwrite those existing themes.

### Component-name warning

A layer or component inside an Acanthus frame may still contain a name such as:

`Stopwatch Professional v2/...`

That name indicates its origin or reuse history. It does not make the Professional v2 appearance authoritative.

Always use:

1. the rendered appearance of the enclosing Acanthus frame
2. the actual fills, strokes, fonts, effects, geometry, and spacing extracted from that Acanthus frame
3. the explicit Acanthus requirements in this prompt

Do not follow the original appearance of a reused component master when it conflicts with the rendered Acanthus frame.

### Required preflight confirmation

Before modifying local application code:

1. Resolve every Acanthus target frame by name.
2. Report the resolved name and node ID.
3. Confirm that each visual source is inside a section whose name begins with:
   `Theme Study / Acanthus`
4. Confirm that `get_design_context` will be called only on those Acanthus visual-source frames.
5. Confirm that non-Acanthus frames will be used only for behavior and state coverage.

If an expected Acanthus frame is missing, ambiguous, or located outside an Acanthus section, stop before modifying code and report the exact missing or ambiguous frame.

### Controller and Timer Workspace

Page:

`03 - Controller and Timer Workspace`

Acanthus section:

`Theme Study / Acanthus`

Main frame:

`Controller / Acanthus POC / Running Stopwatch`

Known frame ID:

`88:879`

Also inspect the full original Controller section to understand all omitted states, including:

- empty state
- several timers
- paused timer
- countdown
- clock
- timecode
- laps
- minimum width
- combined overlay
- inspector access

### Settings and Overlay Inspector

Page:

`04 - Settings and Overlay Inspector`

Acanthus section:

`Theme Study / Acanthus / Settings and Overlay Inspector`

Frames:

- `Settings / Acanthus / 01 Overlay and Position`
- `Settings / Acanthus / 02 Appearance and Background`
- `Settings / Acanthus / 03 Light Ring, Behavior and Application`

Known IDs:

- `105:368`
- `110:830`
- `114:367`

Also inspect the original Settings section for minimum-width behavior and all existing setting states.

### Project Records

Page:

`06 - Project Records`

Acanthus section:

`Theme Study / Acanthus / Project Records`

Frame:

`Project Records / Acanthus / Populated`

Known ID:

`105:2490`

Also inspect the original Records section for:

- empty state
- filtered state
- active locked record
- persistence warning
- narrow layout
- pagination

### Analytics Dashboard

Page:

`05 - Analytics Dashboard`

Acanthus section:

`Theme Study / Acanthus / Analytics Dashboard`

Frames:

- `Analytics / Acanthus / 01 Seven Days`
- `Analytics / Acanthus / 02 Thirty Days`

Known IDs:

- `105:2494`
- `110:322`

Also inspect the original Analytics section for:

- Today
- 7 days
- 30 days
- All time
- project filter
- no data
- loading
- stale state
- narrow layout

### Dialogs

Page:

`07 - Dialogs and Shortcuts`

Acanthus section:

`Theme Study / Acanthus / Dialogs`

Frames:

- `Dialog / Acanthus / 01 Choose or Create Project`
- `Dialog / Acanthus / 02 Add Project Record`

Known IDs:

- `107:2577`
- `107:2578`

Use these as the shared visual language for all dialogs, including:

- project chooser
- inline project creation
- project validation
- add record
- edit record
- record validation
- delete confirmation
- shortcut editor
- shortcut capture
- shortcut conflict confirmation

### Floating Overlay States

Page:

`08 - Floating Overlay States`

Acanthus section:

`Theme Study / Acanthus / Floating Overlay States`

Inspect the current child frames at execution time.

The final corrected structure should be represented by frames similar to:

- `Overlay / Acanthus / 01 Resting`
- `Overlay / Acanthus / 02 Hover Controls`
- `Overlay / Acanthus / 03 Transparency Range`

Names or IDs may differ slightly after editing. Use the latest corrected frames in that section.

Do not implement the obsolete concept that shows:

- project name above the time
- a permanent status header
- mode or shortcut labels inside the floating clock
- a list of all timers in combined mode

## Shared structure requirement

The new Figma structure must become the shared layout for all themes.

Use one common XAML structure for:

- Controller
- Settings
- Project Records
- Analytics
- Dialogs
- Floating Overlay

Apply each theme through `DynamicResource`, resource dictionaries, templates, and theme-aware styles.

The existing themes keep their own:

- colors
- typography
- radii
- shadows
- Pixel Deck geometry
- overlay treatment
- background behavior

Acanthus adds its own:

- parchment and ivory surfaces
- muted sage
- deep olive
- antique gold
- serif display typography
- restrained acanthus ornaments

Acanthus ornaments must not appear in the other themes.

## Theme implementation

### Add Acanthus to the theme catalog

Update the existing theme catalog and manager so that:

- `Acanthus` is a stable theme name
- matching is case-insensitive
- Acanthus is included in the theme selector
- Acanthus persists in settings
- Acanthus loads `Themes/Acanthus.xaml`
- native WPF theme mode is Light for Acanthus
- existing legacy aliases continue to normalize exactly as before
- unknown values keep the current safe fallback behavior

Do not rename or remove existing themes.

### Add the Acanthus resource dictionary

Create:

`StopwatchOverlay/Themes/Acanthus.xaml`

Extract final values from Figma.

Expected visual direction:

- parchment canvas near `#F3EFE5`
- warm ivory near `#FBF8F1`
- soft stone near `#E5DDCF`
- muted sage near `#D5DDCF`
- deep olive near `#445140`
- antique gold near `#B08A4D`
- soft gold near `#D1BC8D`
- charcoal near `#2C2924`
- muted text near `#706A61`
- restrained green near `#4F755A`
- restrained burgundy near `#8A3E45`

Use the actual Figma values when they differ.

### Shared semantic resources

Prefer existing resource keys where they already express the correct role.

Add theme-neutral semantic resources only where needed, such as:

- application header brush
- navigation rail brush
- selected navigation brush
- display heading font
- table header brush
- chart series brushes
- dialog surface brush
- overlay inner rule
- overlay toolbar surface
- ornament stroke
- ornament visibility
- active item border
- subtle divider
- focus border

Every new shared resource key must have a valid value in all five themes.

The app must be able to switch themes live without missing-resource errors.

## Acanthus ornaments

Extract the approved ornamental vectors from Figma rather than redrawing approximate leaves.

Useful existing nodes include:

- crest near the application title
- timer-hero corner flourishes
- botanical divider

Implement reusable vector resources using native WPF types such as:

- `Geometry`
- `StreamGeometry`
- `Path`
- `DrawingImage`
- `ControlTemplate`
- `DataTemplate`

Do not add a large SVG runtime package just for the ornaments.

Use ornaments selectively:

- application header
- focal timer hero
- selected dialog title area
- floating-overlay border

Do not place ornaments inside repeated table rows, chart plotting areas, switches, text fields, or pagination.

## Typography

Use theme font resources.

For Acanthus, prefer:

- `Cormorant Garamond` for display headings
- `Inter` for interface text
- `Cascadia Mono` for timer and numeric data

Provide reliable Windows fallbacks:

- `Georgia`
- `Segoe UI`
- `Consolas`

Do not add font binaries unless their redistribution rights are already verified.

## Controller implementation

Replace the old vertically stacked controller with the new shared structure.

At normal desktop width, implement:

- application header
- left timer rail
- New timer action
- active and inactive timer cards
- combined-overlay status
- active timer workspace
- project chip
- running and active states
- Stopwatch, Clock, Countdown, and Timecode selector
- timer hero
- Start or Pause
- Lap
- Reset
- Show overlay
- contextual content
- shortcut or status footer

The contextual area must support all existing modes:

- Stopwatch with laps
- Clock
- Countdown classic
- Countdown smart input
- Countdown validation
- Timecode
- empty timer workspace

Preserve existing timer state and use `TimerSessionManager` as the single source of truth.

### Responsive controller

Preserve the current minimum window support.

At narrow width:

- do not squeeze the wide two-column layout until it clips
- use compact selectors or stacked content
- preserve every primary action
- reuse the same timer state and command handlers

Do not duplicate timer logic for the compact layout.

## Settings and Overlay Inspector implementation

Create one dedicated settings experience based on the three Figma frames.

It should include:

- live overlay preview
- settings navigation
- Overlay and Position
- Appearance and Background
- Light Ring
- Behavior
- Application
- theme selection

Preserve every existing setting:

- display
- screen position
- custom position
- show overlay
- click-through
- hide from capture
- text color
- outline color
- font
- time format
- text size
- outline thickness
- background opacity
- background selection
- background strength
- add custom background
- remove custom background
- light ring enabled
- light ring brightness
- light ring width
- hide ring from capture
- auto-start
- REC indicator
- blink colon
- start with Windows
- theme

Use one source of truth for `AppSettings`.

The live preview should share the same rendering logic as the real overlay where practical.

After the new settings experience is complete, remove the old large embedded settings expander from the Controller so there is one clear settings UI.

## Project Records implementation

Update `ProjectRecordsWindow` to the new shared structure.

Preserve:

- project filter
- Refresh
- Add record
- three summary cards
- populated records
- active running record lock
- edit
- delete
- pagination
- saved state
- persistence warning
- retry
- empty state
- narrow layout

Use the Acanthus visual language only when the Acanthus theme is selected.

Keep the table highly readable. Do not add leaf decoration to every row.

## Analytics implementation

Update `ProjectDashboardWindow` to the new shared dashboard structure.

Preserve:

- Today
- 7 days
- 30 days
- All time
- project filter
- Refresh
- link to Records
- KPI summaries
- time by project
- daily totals
- timeline lanes
- session details
- long-range activity view
- no-data state
- loading state
- stale state
- narrow layout
- existing aggregation semantics

Use theme-aware chart colors and direct labels so charts do not rely only on color.

## Dialog implementation

Update the existing dialogs to use one shared, theme-aware dialog language.

Apply the new structure and styles to:

- project chooser
- inline project creation
- record editor
- record validation
- delete confirmation
- shortcut editor
- shortcut capture
- shortcut conflict confirmation

Preserve:

- owner relationship
- default button
- Cancel
- Escape
- Enter
- tab order
- validation behavior
- accessibility names
- read-only states
- destructive confirmation semantics

Do not create separate Acanthus-only dialog classes.

## Floating Overlay implementation

The floating clock structure is critical.

The resting floating timer must contain only:

```text
Timer value
Project name below the timer
```

Optional content:

- REC indicator
- active border
- timer outline
- minimal theme-specific border or ornament

Do not permanently show:

- project name above the time
- Running or Paused text
- Stopwatch or Clock label
- shortcut text
- a list of all timers

### Project name

Place the project name directly below the timer.

When the timer has no project name:

- collapse the name row
- reclaim the unused height

### Hover controls

On hover, show a separate popup below the timer containing exactly three icon-only controls:

- Close
- Pause or Resume
- Reset

The toolbar must:

- remain outside the measured timer surface
- not resize the floating window
- not change the anchored screen position
- close when click-through is enabled
- remain fully opaque
- retain tooltips
- preserve no-activate behavior

### Background opacity

Background opacity must affect only the timer background surface.

It must not fade:

- timer text
- project name
- text outline
- border
- active state
- REC indicator
- ornament
- hover toolbar
- hover icons

Do not apply opacity to the whole overlay container.

Use a separate fully opaque resource for the hover toolbar surface.

### Combined mode

Combined mode continues to show only the currently active timer in the same single-timer shell.

Do not turn combined mode into a multi-row timer list.

Other timers continue running independently.

Cycling the active timer updates the shared overlay content.

Preserve previous per-screen positions and visibility when separating overlays again.

## Existing theme compatibility

After the structure update, verify all existing themes.

### Midnight

Keep its restrained dark identity and current safe fallback behavior.

### Daylight

Keep its light neutral identity.

### Pixel Deck Night

Keep its existing pixel geometry, shadows, borders, and night palette.

### Pixel Deck Day

Keep its existing pixel geometry, shadows, borders, and day palette.

The new shared structure must not flatten Pixel Deck into a generic rounded interface.

## Persistence and data safety

Do not reset or discard:

- selected theme
- shortcuts
- application backgrounds
- custom imported backgrounds
- timer workspace
- active timer
- laps
- overlay positions
- combined-overlay state
- project names
- project history
- records
- application options

Adding Acanthus must be backward compatible with current settings files.

Do not change project-history or workspace formats unless a backward-compatible addition is strictly necessary.

## Build and tests

Before modifications, run:

```text
dotnet build
dotnet test
```

During implementation, build after each major section.

Before completion, run:

```text
dotnet build -c Release
dotnet test -c Release
```

Add focused tests for:

- all five themes in the catalog
- Acanthus normalization
- old theme normalization
- Acanthus persistence
- resource-key completeness for all themes
- background-opacity clamping
- overlay project-name collapse
- combined mode showing only the active timer
- any new pure responsive-layout helper
- no regression in settings, workspace, or project-history persistence

Do not delete or weaken meaningful tests.

## Visual validation

When the environment supports running the Windows GUI, compare the implementation against Figma.

Validate:

### All themes

- Controller
- Settings
- Project Records
- Analytics
- Project chooser
- Record editor
- resting overlay
- hover overlay

### Important states

- running timer
- paused timer
- multiple timers
- empty controller
- countdown classic
- countdown smart
- clock
- timecode
- laps
- narrow controller
- all settings categories
- filtered records
- active locked record
- empty records
- 7-day analytics
- 30-day analytics
- no-data analytics
- loading analytics
- shortcut editor
- validation
- deletion
- named overlay
- unnamed overlay
- REC
- click-through
- low background opacity
- high background opacity
- combined mode
- multi-monitor placement

Also verify live switching among all five themes while multiple windows are open.

If the environment cannot launch a Windows GUI, report that visual validation still needs to be performed manually. Do not claim it was completed.

## Implementation order

Follow this order:

1. Inspect local project
2. Baseline build and tests
3. Figma node discovery
4. Read all main Acanthus frames with `get_design_context`
5. Extract exact theme values and vector assets
6. Add Acanthus theme catalog support
7. Add `Themes/Acanthus.xaml`
8. Complete semantic resource keys across all themes
9. Implement reusable application and dialog shells
10. Implement Controller structure
11. Implement Settings and Overlay Inspector
12. Implement Project Records
13. Implement Analytics
14. Implement dialogs and shortcuts
15. Implement corrected floating overlay
16. Implement responsive states
17. Test all themes
18. Release build
19. Visual comparison and refinement
20. Final report

Maintain a resumable local state file:

`design-to-code-state-acanthus.json`

Track:

- Figma nodes inspected
- assets downloaded
- files changed
- phases completed
- build and test results
- visual checks completed
- remaining issues

## Acceptance criteria

The task is complete only when:

- Acanthus is selectable
- Acanthus persists after restart
- Midnight remains available
- Daylight remains available
- Pixel Deck Night remains available
- Pixel Deck Day remains available
- all five themes use the new shared structure
- no duplicate Acanthus-only window implementation exists
- Acanthus ornaments appear only in Acanthus
- all existing features remain available
- the dedicated Settings structure is implemented
- records and analytics use the new structure
- dialogs use the new shared shell
- floating overlay shows the timer first and project name below
- hover controls are external and icon-only
- opacity affects the background only
- combined mode shows the active timer only
- no missing dynamic resources exist
- documented minimum sizes do not clip essential controls
- build succeeds
- tests pass
- visual validation is either completed or clearly listed as pending

## Final report

At completion, report:

1. Figma nodes used
2. files added
3. files modified
4. shared structural changes
5. Acanthus theme implementation
6. compatibility work for old themes
7. floating-overlay corrections
8. tests added or updated
9. build and test commands with actual results
10. visual states checked
11. remaining manual checks
12. any intentional WPF adaptation from Figma and why

Do not claim completion for work that was not actually built, tested, or visually checked.
