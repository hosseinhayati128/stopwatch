# Implement Pirate Navigator Theme for Stopwatch

## Goal

Implement a new pirate / nautical pixel-art visual theme for the Stopwatch application.

This is a theme implementation task only.

Do not redesign functionality.
Do not remove features.
Do not modify timer logic, persistence, shortcuts, analytics, or project tracking.

## Reference images

Inspect all images in:

```
C:\Users\h128\Documents\Projects\stopwatch-main\sample
```

Use them as visual references.

The references show:
- pirate settings interface
- pirate dashboard
- pixel compass logo
- pirate stopwatch controller

Do not use screenshots as UI assets. Recreate the style with native WPF resources.

## Theme architecture

Add a new selectable theme.

Keep all existing themes unchanged:

- Midnight
- Daylight
- Pixel Deck Night
- Pixel Deck Day
- Acanthus
- other existing themes

Do not create pirate-only windows.

Use:
- shared layouts
- resource dictionaries
- styles
- templates
- reusable assets

## Theme feeling

Create:

- pirate navigation console
- explorer map interface
- wooden ship instrument panel
- handcrafted nautical productivity tool

Avoid:

- childish pirate decorations
- game HUD overload
- unreadable fantasy fonts
- random stickers everywhere

The application must remain a professional stopwatch.

## Visual language

Materials:

- dark walnut wood
- parchment paper
- leather
- bronze
- antique gold
- copper

Colors:

Wood:
- dark brown
- mahogany
- charcoal

Paper:
- parchment beige
- warm ivory

Metal:
- brass
- bronze
- antique gold

Accent:
- turquoise ocean glow
- emerald navigation light
- muted red warning

Text:
- warm cream
- parchment white
- dark ink brown

## Assets

Create reusable theme assets:

### Compass mark

Use the pixel compass reference as inspiration.

Use it inside the application only.

Do not replace the executable icon.

### Frames

Create reusable styles for:

- headers
- panels
- cards
- buttons
- dialogs
- overlay

Style:
- wooden frame
- brass border
- parchment inner surface
- subtle shadow

### Map surfaces

Create reusable parchment/map style resources.

Do not use the screenshots as backgrounds.

## Screens affected

Apply the theme to:

1. Controller and Timer Workspace
2. Settings and Overlay Inspector
3. Analytics Dashboard
4. Project Records
5. Dialogs
6. Floating Overlay

Do not change layouts or business logic.

## Controller

Use the pirate stopwatch reference.

Keep:

- timer list
- timer modes
- Start/Pause
- Reset
- Lap
- projects
- shortcuts

Visual changes only:

- wood frame
- parchment workspace
- brass controls
- compass accents

Timer remains dominant.

## Settings

Use:

- wooden shell
- parchment sheets
- scroll-like navigation
- brass sliders
- nautical switches

Keep all settings and controls functional.

Do not replace sliders or scrollbars with images.

## Dashboard and Records

Use:

- parchment cards
- brass frames
- map-inspired styling

Keep charts readable.

Do not sacrifice data clarity.

## Floating Overlay

Keep the existing structure:

```
Timer value
Project name below
```

Hover:

```
Close
Pause/Resume
Reset
```

Only change:

- colors
- borders
- typography
- ornament

Do not add large decorations behind the timer.

## Implementation

Before coding:

1. Inspect theme architecture.
2. Inspect current resource dictionaries.
3. Inspect current windows.
4. Inspect reference images.
5. Create a plan.

Implement:

1. theme catalog
2. pirate resource dictionary
3. control styles
4. window styles
5. overlay style
6. validation

Use native WPF resources:
- Geometry
- DrawingImage
- vectors
- resource dictionaries

Do not add unnecessary dependencies.

## Validation

Verify:

- existing themes still work
- pirate theme switches correctly
- settings persist
- restart restores theme
- no missing resources
- no broken controls
- sliders and scrolling work
- timer remains readable

Run:

```
dotnet build
dotnet test
```

## Final report

Report:

1. files changed
2. new theme resources
3. assets created
4. reference images used
5. screens updated
6. confirmation existing themes remain unchanged
7. build results
8. test results
