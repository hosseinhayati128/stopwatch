# Fix and Complete Acanthus Theme Implementation Only

## Mission

Repair and complete the **Acanthus theme implementation** in the current local Stopwatch Overlay application.

Important: this task is ONLY about the Acanthus theme.

Do not modify:

- Midnight theme
- Daylight theme
- Pixel Deck Night theme
- Pixel Deck Day theme
- their colors
- their layouts
- their hover states
- their resource values
- their behavior

The other themes are already considered independent products. Any shared code change must be proven to be theme-neutral and must not visually alter them.

## Figma source

Use this Figma file as the visual source:

https://www.figma.com/design/q1GhB2cTVZiVWhbajEGT4K/StopWatch?node-id=13-6&t=zBkEwoSPfi2Eitki-1

Use Figma as read-only reference.

Do not edit Figma.

## Critical Figma rule

This Figma file contains multiple themes and many panels.

Only some panels are Acanthus.

Do not assume every screen in Figma belongs to Acanthus.

The only authoritative visual sources are frames inside sections whose names contain:

```
Theme Study / Acanthus
```

Use those frames for:

- colors
- typography
- spacing
- borders
- shadows
- ornamentation
- component appearance
- visual hierarchy

Do NOT copy visual styles from:

- Controller / Default Dark
- Controller / Default Light
- Professional v2
- Pixel Deck
- other non-Acanthus frames

Non-Acanthus frames may only be used to understand:

- available application states
- missing functionality
- responsive behavior
- required controls

They are NOT visual references.

## Current app assumption

The app already works after stability repairs.

Do not perform a general refactor.

Do not fix unrelated bugs.

Do not redesign the application structure.

Focus only on making the Acanthus theme correctly match Figma.

## First step: inspect before changing

Before editing:

1. Inspect the current theme architecture.
2. Identify how themes are selected.
3. Identify the current Acanthus implementation.
4. Build the application.
5. Run tests.
6. Record the current state.

Inspect:

- AppThemeManager
- AppThemeCatalog
- AppSettings
- Theme resource dictionaries
- Controller window styles
- Settings styles
- Records styles
- Analytics styles
- Dialog styles
- Overlay styles
- any Acanthus-specific files

Search for:

- Acanthus
- hardcoded colors
- hardcoded fonts
- ornament resources
- theme-specific styles
- resources missing from Acanthus.xaml

## Acanthus scope

Implement Acanthus only as:

- a selectable theme
- a resource dictionary
- theme-specific styles
- theme-specific ornaments
- theme-specific typography
- theme-specific surfaces

Do not create:

- Acanthus-only windows
- duplicated controllers
- duplicated settings
- duplicated dialogs
- separate Acanthus business logic

The application structure remains shared.

## Figma inspection requirements

Before implementing each screen:

Use the Acanthus Figma frame as the source.

Inspect:

### Controller

Use:

```
Controller / Acanthus POC / Running Stopwatch
```

Expected style:

- parchment and ivory surfaces
- muted sage areas
- deep olive accents
- antique gold rules
- classical typography
- restrained botanical decoration

### Settings

Use only:

```
Settings / Acanthus / 01 Overlay and Position
Settings / Acanthus / 02 Appearance and Background
Settings / Acanthus / 03 Light Ring, Behavior and Application
```

### Project Records

Use:

```
Project Records / Acanthus / Populated
```

### Analytics

Use:

```
Analytics / Acanthus / 01 Seven Days
Analytics / Acanthus / 02 Thirty Days
```

### Dialogs

Use:

```
Dialog / Acanthus / 01 Choose or Create Project
Dialog / Acanthus / 02 Add Project Record
```

### Floating Overlay

Use only the Acanthus floating overlay frames.

The final product behavior must remain:

```
Timer value
Project name below timer

Hover:
Close
Pause/Resume
Reset
```

Do not introduce:

- multi-row combined timers
- project headers above the timer
- permanent status labels
- shortcut labels inside the floating clock

## Theme resource implementation

Create or repair:

```
Themes/Acanthus.xaml
```

It must define all resources required by the application.

Do not put Acanthus colors directly into shared XAML.

Use semantic resources.

Examples:

- ApplicationBackgroundBrush
- SurfaceBrush
- CardBrush
- HeaderBrush
- PrimaryTextBrush
- SecondaryTextBrush
- AccentBrush
- GoldRuleBrush
- BorderBrush
- HoverBrush
- PressedBrush
- OverlayBrush
- OverlayToolbarBrush
- OrnamentBrush

The other themes must continue using their own values.

## Acanthus visual language

Extract final values from Figma.

Expected direction:

Colors:

- warm parchment
- ivory
- stone
- muted sage
- deep olive
- antique gold
- charcoal text

Typography:

- serif display headings
- clean sans-serif interface text
- monospaced timer values

Ornaments:

Use only:

- small crest
- subtle corner flourishes
- restrained botanical divider

Do not use:

- dense floral wallpaper
- decorative leaves everywhere
- ornaments inside tables
- ornaments inside charts
- ornaments inside every button

Acanthus should feel:

- elegant
- classical
- premium
- calm
- professional

Not:

- fantasy
- wedding invitation
- game interface
- luxury packaging mockup

## Floating overlay Acanthus rules

The floating overlay must stay practical.

Correct structure:

```
Timer
Project name
```

Hover:

```
[Close] [Pause/Resume] [Reset]
```

Rules:

- project name below time
- timer remains dominant
- controls appear in a separate hover popup
- opacity affects only background
- text remains readable
- toolbar remains opaque
- no multi-timer list

## Preserve all existing behavior

Do not change:

- timer logic
- project tracking
- records
- analytics calculations
- shortcuts
- persistence
- overlay positioning
- click-through
- light ring behavior
- background import
- recovery logic

Only change appearance and theme resources.

## Verification

After implementation:

Build:

```
dotnet build
dotnet test
```

Then manually verify Acanthus only:

- Controller
- Settings
- Records
- Analytics
- Dialogs
- Floating overlay

Check:

- all controls readable
- all text visible
- ornaments aligned
- no missing resources
- no broken hover states
- no layout clipping

Then switch to:

- Midnight
- Daylight
- Pixel Deck Night
- Pixel Deck Day

Confirm they are unchanged.

## Completion report

Report:

1. Acanthus files changed
2. Figma frames used
3. theme resources added
4. ornaments added
5. screens updated
6. confirmation that other themes were not visually changed
7. build result
8. test result
9. remaining differences from Figma
