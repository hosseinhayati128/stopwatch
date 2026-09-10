# Stopwatch Overlay: Professional UI Redesign with Codex and Figma MCP

## Role

Act as a senior product designer, UX architect, design-system designer, and desktop application specialist. Redesign the user interface of the existing **Stopwatch Overlay** Windows application in Figma by using the connected Figma MCP tools.

This is not a generic concept exercise and not a decorative reskin. Build an implementation-aware, high-fidelity design for the real application in the current local project folder.

## Source of truth

Use the application files already available in the current local Codex workspace as the primary source of truth. Do not clone, download, fetch, or search for another copy of the project.

Target Figma Design file:

`https://www.figma.com/design/q1GhB2cTVZiVWhbajEGT4K/Untitled?node-id=0-1&t=K9HM5SQoqV28q8AD-1`

Use this exact Figma file. Extract the file key from the supplied address when a tool requires it. Inspect the file before writing, preserve every existing page and object, and create the professional redesign in a new top-level page or section named:

`Stopwatch Overlay / Professional Redesign v2`

If the placeholder has not been replaced, stop before making any Figma changes and report that the target Figma address is missing. If the supplied file cannot be accessed, report the exact access or permission blocker. Do not create or switch to another Figma file unless explicitly instructed.

Record the target Figma URL and extracted file key in the final report and in the state ledger described below.

## Primary objective

Create a coherent, professional, beautiful, accessible, and user-friendly desktop UI that:

1. Makes starting, pausing, resetting, and switching timers effortless.
2. Makes multiple timers and the active timer immediately understandable.
3. Separates frequent timer actions from infrequent configuration.
4. Makes project tracking and analytics useful without overwhelming the user.
5. Preserves all current functionality and important edge cases.
6. Feels like a polished Windows productivity application.
7. Can realistically be implemented in the current WPF and .NET codebase.
8. Provides native, editable Figma components, variables, layouts, and prototype states.

Do not stop after an audit, mood board, wireframe, or component sample. Continue through the complete high-fidelity redesign, prototype flows, validation, and handoff material unless an actual tool or authentication blocker makes further work impossible.

## Important context from the current product

The application is a Windows WPF desktop tool with these major capabilities:

- Stopwatch, clock, countdown, and timecode modes.
- Multiple independent timers.
- One active timer that receives commands.
- Separate floating overlays or one combined overlay.
- Project assignment and automatic work-session tracking.
- Project analytics for Today, 7 days, 30 days, and All time.
- Detailed project records with add, edit, delete, filtering, and pagination.
- Customizable global keyboard shortcuts.
- Smart countdown text input and classic duration or wall-clock input.
- Overlay display selection and screen position.
- Timer text color, outline, font, format, size, thickness, and opacity.
- Optional REC indicator, blinking colon, auto-start, click-through, and light ring.
- Theme and tiled background selection, including imported images.
- Multi-monitor behavior, screen-capture protection, persistence, and recovery.
- Notification-area operation and Start with Windows.

The current controller places the active timer, actions, behavioral options, a long expanded settings form, laps, and shortcut status in one vertically scrolling window. The redesign must improve this information architecture, not merely restyle the same long form.

## Read these files before designing

Read the relevant implementation and design files before the first Figma write:

### Product and architecture

- `README.md`
- `DEVELOPERS.md`
- `StopwatchOverlay/StopwatchOverlay.csproj`

### Main windows

- `StopwatchOverlay/ControllerWindow.xaml`
- `StopwatchOverlay/ControllerWindow.xaml.cs`
- `StopwatchOverlay/OverlayWindow.xaml`
- `StopwatchOverlay/OverlayWindow.xaml.cs`
- `StopwatchOverlay/ProjectDashboardWindow.xaml`
- `StopwatchOverlay/ProjectDashboardWindow.xaml.cs`
- `StopwatchOverlay/ProjectRecordsWindow.xaml`
- `StopwatchOverlay/ProjectRecordsWindow.xaml.cs`
- `StopwatchOverlay/ProjectRecordEditorWindow.xaml`
- `StopwatchOverlay/ProjectRecordEditorWindow.xaml.cs`
- `StopwatchOverlay/ProjectRecordDeleteWindow.xaml`
- `StopwatchOverlay/TimerNameWindow.xaml`
- `StopwatchOverlay/TimerNameWindow.xaml.cs`
- `StopwatchOverlay/ShortcutsWindow.xaml`
- `StopwatchOverlay/ShortcutsWindow.xaml.cs`

### Existing styles and themes

- `StopwatchOverlay/App.xaml`
- `StopwatchOverlay/AppThemeManager.cs`
- `StopwatchOverlay/AppBackgroundManager.cs`
- `StopwatchOverlay/Themes/Midnight.xaml`
- `StopwatchOverlay/Themes/Daylight.xaml`
- `StopwatchOverlay/Themes/PixelDeck.xaml`
- `StopwatchOverlay/Themes/PixelDeckDay.xaml`

### Current visual references

- `controller-window.png`
- `project-dashboard.png`
- `promo.png`
- `design/figma/renders/`
- `design/figma/README.md`
- `design-system-state-stopwatch-pixel.json`

Use the existing screenshots to understand the current density and hierarchy. Use the XAML and code-behind to understand actual states, commands, dependencies, and implementation constraints.

## Non-negotiable constraints

### Preserve behavior

- Do not remove existing product capabilities.
- Do not rename or reinterpret actions in ways that change their meaning.
- Do not assume all timers stop when a different timer becomes active.
- Clearly distinguish the selected or active timer from timers that are merely running.
- Treat combined overlay mode as a presentation mode, not a timer-state change.
- Preserve unnamed timers that stay out of project reports.
- Preserve active project records as read-only until their timer is paused or stopped.
- Preserve click-through behavior and the fact that it disables overlay mouse interaction.
- Preserve separate and combined overlay states.
- Preserve keyboard-first operation.

### Design only in this task

- Do not modify production XAML, C#, persistence logic, or tests during this design task.
- You may add or update design documentation and a Figma state ledger.
- Provide implementation mapping and recommendations, but leave code changes for a separate implementation task.

### Use real Figma structures

- Build native, editable Figma frames, auto-layouts, components, component sets, variables, text styles, and effect styles.
- Do not substitute a collection of flattened SVG boards for a finished Figma design.
- Do not use a single giant generated image as the deliverable.
- Do not create the whole file in one large, fragile MCP call.
- Work incrementally and validate after each meaningful step.
- Preserve all existing Figma content.
- Use deterministic, descriptive node names.
- Return and retain all created or modified node IDs when the tool requires them.
- Load fonts before changing text through the Figma Plugin API.
- Use auto-layout for structurally related content.
- Use design variables before building reusable components.
- Use component instances in screens instead of duplicating visual primitives.

### Do not repeat the previous visual direction

The existing Pixel Deck work is a historical reference and functional inventory only. It is not the visual target for this redesign.

Do not use:

- Pirate, nautical, anime, game, or pixel-art styling.
- Pixel borders, stepped corners, chunky offset shadows, or arcade typography.
- Decorative tiled patterns as the dominant application shell.
- Gold and purple fantasy-game palettes.
- Heavy skeuomorphism.
- Excessive glassmorphism, neon glows, or decorative gradients.
- Emoji as interface icons.
- Generic web-dashboard styling that ignores Windows desktop conventions.
- A card around every small group of content.
- Very large empty areas that reduce desktop productivity.
- Tiny low-contrast text or unlabeled icon-only actions for important commands.

The existing Pixel Deck pages and assets must remain untouched.

## Product design direction

Create a calm, premium, focused Windows productivity interface. Draw inspiration from the clarity, hierarchy, and interaction quality of excellent productivity tools, but do not copy any product directly.

The result should feel:

- Native to Windows 11 while remaining usable on Windows 10.
- Precise and efficient rather than playful.
- Modern without looking like a mobile app enlarged for desktop.
- Friendly without becoming visually noisy.
- Compact enough for frequent use, with generous spacing around high-value actions.
- Consistent across controller, analytics, records, dialogs, and overlays.
- Suitable for long daily sessions.
- Legible at 100%, 125%, 150%, and 200% Windows scaling.

### Suggested visual foundation

Use these values as a starting direction, then adjust only when contrast or visual balance requires it.

#### Dark mode seed

- Canvas: `#0E1116`
- Surface: `#151A21`
- Raised surface: `#1B222C`
- Hover surface: `#232D39`
- Border: `#2C3743`
- Strong text: `#F4F7FA`
- Secondary text: `#9EABB7`
- Accent: `#42B9E8`
- Accent pressed: `#2D94BC`
- Success: `#3DB47B`
- Warning: `#D99A32`
- Danger: `#E05A67`

#### Light mode seed

- Canvas: `#F4F7F9`
- Surface: `#FFFFFF`
- Raised surface: `#F8FAFB`
- Hover surface: `#EDF3F6`
- Border: `#D8E1E7`
- Strong text: `#17212B`
- Secondary text: `#64727E`
- Accent: `#167FA8`
- Accent pressed: `#116988`
- Success: `#24845A`
- Warning: `#A96A08`
- Danger: `#C83E4D`

Use one restrained blue-cyan accent family. Reserve semantic colors for actual status and feedback.

### Typography

Use Windows-native or reliably available fonts:

- Interface: `Segoe UI Variable`, with `Segoe UI` as fallback.
- Timer and numeric data: `Cascadia Mono`, with `Consolas` as fallback.

Recommended hierarchy:

- Display timer: 40 to 56 px depending on frame.
- Page title: 24 px.
- Section title: 16 to 18 px.
- Body and controls: 14 px.
- Supporting text: 12 to 13 px.
- Avoid uppercase paragraphs. Use uppercase only for very short KPI labels when it improves scanning.

### Geometry and spacing

- Base grid: 4 px.
- Primary spacing rhythm: 8, 12, 16, 24, 32.
- Standard control height: 36 or 40 px.
- Primary action height: 44 px where space allows.
- Small icon action: at least 32 by 32 px.
- Corner radii: 6 px for controls, 8 px for cards, 10 to 12 px for large panels and dialogs.
- Borders: generally 1 px.
- Shadows: subtle and used only to express elevation.
- Prefer separators, spacing, and background levels over excessive nested cards.

## Recommended information architecture

Use this as the default architecture unless inspection of the local application files reveals a stronger implementation-aware alternative.

### Controller window

Redesign the controller as a focused two-pane timer workspace:

1. **Left timer rail**
   - App identity and compact navigation.
   - `New timer` primary affordance.
   - List of all timers.
   - Each timer item shows project or unnamed state, current value, mode, running or paused status, and overlay visibility.
   - Running state and active-selection state must be visually different.
   - Support long names, several timers, empty state, overflow, and keyboard focus.
   - Include a compact combined-overlay status/control when relevant.

2. **Main active-timer workspace**
   - Active project selector or project chip near the top.
   - Segmented mode control for Stopwatch, Clock, Countdown, and Timecode.
   - Large timer display with an explicit status label.
   - Primary Start, Pause, or Stop action.
   - Secondary Lap and Reset actions.
   - Overlay visibility control.
   - Hotkey hints as compact keycaps next to labels, not embedded as long text inside every button.
   - Contextual countdown configuration only when Countdown is selected.
   - Contextual lap history only when laps exist or Stopwatch is active.
   - Clear empty state when there is no timer.

3. **Inspector or settings access**
   - Move infrequent configuration out of the main vertical flow.
   - Use a dedicated right-side inspector, settings page, or drawer.
   - Settings must not remain one permanently expanded, very long form under the timer.
   - Preserve live preview where valuable, especially for overlay appearance.

4. **Command and app access**
   - Replace the dated menu-heavy feel with a restrained command bar or overflow menu while retaining keyboard access.
   - Keep Analytics, Records, Shortcuts, and app-level settings easy to find.
   - Keep tray behavior and closing semantics clear through copy or an overflow command.

### Settings organization

Organize settings by user intent:

- **Overlay**
  - Display or monitor.
  - Position, preferably a visual 3 by 2 position picker plus Custom.
  - Show or hide behavior.
  - Click-through.
  - Hide from screen capture where supported.

- **Timer appearance**
  - Live overlay preview.
  - Text color.
  - Outline color and thickness.
  - Font.
  - Time format.
  - Size.
  - Surface opacity.
  - REC indicator.
  - Blink colon.

- **Background**
  - `None` or clean default first.
  - Pattern and imported-image thumbnails.
  - Strength control.
  - Add and remove custom image actions.
  - Keep patterns optional and secondary.

- **Light ring**
  - Master toggle.
  - Reveal brightness, width, and capture visibility only when enabled.

- **Behavior**
  - Auto-start on show.
  - Other timer-specific behavior already supported by the code.

- **Application**
  - Start with Windows.
  - Theme.
  - Shortcut settings entry.
  - Any app-level recovery or tray explanation that already exists.

Show dependencies through progressive disclosure. Disabled states must explain why they are disabled.

### Analytics dashboard

Redesign the dashboard as a clean analytical workspace:

- Persistent page title, project filter, date range segmented control, and Refresh.
- Four KPI summaries with clear labels and useful secondary context.
- Time by project with readable labels and precise values.
- Daily totals with axis labels or direct labels.
- Daily timeline with 00:00 to 24:00 context and separate lanes for overlaps.
- Session details as a structured list or table.
- Project Records as a clear navigation action, not a visually oversized promotional card.
- Empty, loading, stale, and no-data states.
- A visible explanation that simultaneous timers are added rather than deduplicated.
- Consistent project colors across all charts and records.
- Color-blind-safe distinctions and direct labels so color is not the only encoding.
- Avoid nested cards around every section. Use a clear page grid and section boundaries.

### Project records

Use a desktop-friendly, scannable records experience:

- Header with Add record, project filter, refresh, and update status.
- Summary values for total time, record count, and latest activity.
- A structured table or strong row layout with:
  - Project.
  - Date.
  - Start.
  - End.
  - Duration.
  - Running or completed state.
  - Edit and delete actions where allowed.
- Active records must be visibly locked and explain that the timer must be paused first.
- Clear pagination.
- Persistence warning.
- Empty state with a direct Add record action.
- Do not invent unsupported filtering or export features as required functionality. Optional concepts may be annotated separately.

### Shortcut editor

Improve the shortcut editor by grouping commands:

- Timer creation and selection.
- Timer control.
- Overlay control.
- Projects and reports.

Include:

- Clear key-capture state.
- Current shortcut rendered as keycaps.
- Unassigned state.
- Conflict and invalid-combination feedback.
- Clear action per row.
- Reset to defaults.
- Cancel and Save.
- Keyboard focus and tab order annotations.

### Project chooser

Design a compact, clear chooser that supports:

- Existing project selection.
- `No project` as an explicit option.
- Inline project creation.
- Validation.
- Cancel.
- `Use project` or `Create timer`, depending on context.
- Explanation that unnamed timers do not appear in reports.
- Long project names and an empty project list.

### Project record editor

Design add and edit variants with:

- Project selection or new project creation.
- Clear Start and End sections.
- Date and 24-hour time inputs.
- Duration preview.
- Inline validation.
- Invalid time range.
- Future endpoint.
- Daylight-saving-time edge case messaging where the code supports it.
- Cancel and Add or Save.
- Keyboard-first behavior.

### Delete confirmation

Provide a focused destructive confirmation with:

- Project, date, time, and duration summary.
- A clear permanent-action warning.
- Safe default focus.
- Cancel and Delete record.
- Danger styling that is strong but not visually theatrical.

### Floating overlays

Design implementation-realistic variants for:

- Stopwatch.
- Clock.
- Countdown.
- Timecode.
- Named and unnamed timer.
- Running and paused.
- Active and inactive.
- REC on and off.
- Click-through on and off.
- Separate and combined mode.
- Clean surface, transparent surface, and user-selected background.
- Hover toolbar with Close, Pause or Resume, and Reset.
- Compact mode.
- Long project name.
- Small and large timer font sizes.
- Readability over light, dark, and visually busy backgrounds.

The overlay must remain lightweight and should not look like a miniature dashboard. Keep the active indicator subtle. Do not make critical information depend on hover alone. Annotate keyboard and context-menu alternatives already available in the product.

## Required Figma page structure

Create or reuse these pages inside the professional redesign area:

1. `00 - Cover and Product Audit`
2. `01 - Foundations`
3. `02 - Components`
4. `03 - Controller and Timer Workspace`
5. `04 - Settings and Overlay Inspector`
6. `05 - Analytics Dashboard`
7. `06 - Project Records`
8. `07 - Dialogs and Shortcuts`
9. `08 - Floating Overlay States`
10. `09 - Prototype Flows and Handoff`

Use sections inside each page to keep states organized.

## Required high-fidelity frames

Create at least the following implementation-ready frames.

### Controller

- Default dark mode at approximately 1040 by 720.
- Default light mode.
- One running timer.
- One paused timer.
- Several timers with different running states.
- No-timer empty state.
- Countdown with classic input.
- Countdown with smart input and interpretation preview.
- Clock mode.
- Timecode mode.
- Laps populated.
- Combined-overlay state.
- Settings inspector open.
- Compact or minimum-width behavior near the current 560 by 520 minimum.

### Analytics

- Dark mode with realistic populated data.
- Light mode.
- Today.
- 7-day view.
- 30-day view.
- Single-project filter.
- No-data state.
- Narrow behavior near 760 by 560.

### Records

- Populated records.
- Project-filtered records.
- Active locked record.
- Empty state.
- Persistence warning.
- Narrow behavior near 760 by 520.

### Dialogs

- Choose project.
- Create project inline.
- Project chooser validation.
- Add record.
- Edit record.
- Editor validation.
- Delete confirmation.
- Shortcut editor default.
- Shortcut capture.
- Shortcut conflict or invalid state.

### Overlays

- Default.
- Transparent.
- Paused.
- Active.
- Hover controls.
- Click-through.
- Named and unnamed.
- REC.
- Countdown.
- Timecode.
- Combined mode.
- Busy-background readability test.

## Design system requirements

Create a professional, reusable design system before assembling the final screens.

### Variables

At minimum, create semantic variables for:

- Background canvas.
- Background surface.
- Background raised.
- Background hover.
- Background selected.
- Border default.
- Border strong.
- Text primary.
- Text secondary.
- Text disabled.
- Accent default.
- Accent hover.
- Accent pressed.
- On-accent text.
- Success.
- Warning.
- Danger.
- REC.
- Overlay chrome.
- Focus ring.
- Chart project colors.
- Spacing scale.
- Corner-radius scale.
- Border widths.
- Common control heights.

Use Light and Dark modes. Alias semantic variables to primitives where practical. Set appropriate variable scopes. Avoid hardcoded colors in final components when a semantic variable applies.

### Text styles

Create styles for:

- Display timer.
- Page title.
- Section title.
- Body.
- Body strong.
- Label.
- Supporting text.
- KPI value.
- Mono value.
- Keycap or shortcut.

### Effect styles

Create restrained styles for:

- Raised control.
- Dialog or floating panel.
- Overlay chrome.
- Focus state if an effect is useful.

### Core components

Build reusable components and sensible variants for:

- App or window header.
- Navigation item.
- Timer list item.
- New timer action.
- Mode segmented control.
- Primary button.
- Secondary button.
- Ghost button.
- Danger button.
- Icon button.
- Text field.
- Smart countdown field.
- Select or combo field.
- Date field.
- Time field.
- Checkbox.
- Switch or toggle.
- Slider.
- Position picker.
- Theme thumbnail.
- Background thumbnail.
- Status badge.
- Project chip.
- Keycap.
- Tooltip.
- Inline validation.
- Banner or warning.
- KPI summary.
- Chart legend item.
- Record row.
- Empty state.
- Pagination.
- Dialog shell.
- Timer overlay.
- Overlay hover toolbar.

Include relevant states such as default, hover, pressed, focus, selected, disabled, error, running, paused, active, inactive, and locked. Avoid creating an impractically large variant matrix. Use component properties and nested instances where appropriate.

## Interaction and prototype requirements

Create prototype connections for these important flows:

### Timer flow

1. Select a timer.
2. Start it.
3. Pause it.
4. Add a lap.
5. Reset it.
6. Show or hide its overlay.
7. Create a new timer through the project chooser.
8. Switch the active timer while another timer continues running.

### Countdown flow

1. Select Countdown.
2. Choose classic or smart input.
3. Enter a valid value.
4. See the interpretation preview.
5. Show an invalid input state.
6. Start the countdown.

### Settings flow

1. Open the overlay inspector.
2. Change display and position.
3. Change timer appearance.
4. Toggle the light ring and reveal dependent controls.
5. Select a background.
6. Preview the overlay.
7. Close the inspector without losing the active timer context.

### Reporting flow

1. Open Analytics.
2. Change date range.
3. Filter by project.
4. Open Project Records.
5. Add or edit a record.
6. Show active-record locking.
7. Confirm a delete action.

### Overlay flow

1. Hover the overlay to reveal actions.
2. Pause or resume.
3. Reset.
4. Select an inactive timer overlay.
5. Show click-through state.
6. Show combined-overlay mode.

Use prototype interactions to communicate navigation and state transitions. Do not simulate actual timer ticking with hundreds of frames.

## Accessibility and usability requirements

- Meet at least WCAG AA contrast for normal text where applicable.
- Ensure visible keyboard focus on every interactive control.
- Do not communicate state by color alone.
- Keep critical targets at least 32 by 32 px, and prefer 40 to 44 px for primary actions.
- Provide clear labels or tooltips for icon buttons.
- Support long project names, large numeric values, and localization expansion.
- Avoid text smaller than 12 px.
- Define empty, disabled, error, running, paused, loading, and no-data states.
- Annotate expected keyboard navigation and tab order.
- Keep screen-reader-friendly control labels in the handoff notes.
- Verify that the light and dark modes both remain legible.
- Test key frames at 100%, 125%, 150%, and 200% conceptual scaling.
- Avoid hover-only access to essential commands.

## WPF and implementation realism

The design must be feasible in the current WPF application.

- Prefer layouts expressible with Grid, DockPanel, StackPanel, WrapPanel, ScrollViewer, ItemsControl, ListBox, Expander, Popup, and reusable ControlTemplates.
- Avoid web-only patterns that would require a full application rewrite.
- Avoid unsupported blur, complex shader effects, or continuous animation as core requirements.
- Keep native Windows window chrome unless a custom title bar has a clear, implementation-ready benefit.
- Account for resizable windows and the existing minimum sizes.
- Use a layout that degrades cleanly under narrower widths.
- Map components and frames to the current XAML files and resource dictionaries.
- Clearly label optional future enhancements separately from the implementation-ready v1 redesign.

## Figma MCP execution workflow

Before any Figma write, load and follow the Figma MCP instructions and skills available in the environment. When available, this includes:

- `figma-use`
- `figma-generate-design`
- `figma-generate-library`

Use the following workflow.

### Phase 0: Discovery and audit

1. Read the local project files listed above.
2. Inspect the existing Figma file, pages, variables, styles, components, and conventions.
3. Inspect available Figma libraries before searching for components.
4. Identify existing components that can be safely reused.
5. Audit the current UI and document:
   - Current information architecture.
   - Frequent versus infrequent actions.
   - Major usability problems.
   - Functional constraints.
   - Existing visual tokens.
   - Code-to-Figma mapping.
6. Create the `00 - Cover and Product Audit` page.
7. State the exact v1 component and frame scope before creation.

Do not mutate existing Pixel Deck content.

### Phase 1: Foundations

1. Create primitive and semantic variables with Light and Dark modes.
2. Create spacing, radius, sizing, and border variables.
3. Create text and effect styles.
4. Create a clear foundations board with color, type, spacing, geometry, elevation, icon, and accessibility guidance.
5. Validate foundations with metadata and screenshots.

### Phase 2: Components

1. Build components in dependency order.
2. Use auto-layout and variable binding.
3. Create meaningful states and variants.
4. Use consistent naming and descriptions.
5. Validate each component family before building screens.
6. Use an icon library available to the Figma file when appropriate. Do not use emoji.
7. Keep icon style consistent and suitable for Windows desktop UI.

### Phase 3: High-fidelity screens

1. Build wrapper frames first.
2. Assemble screens from component instances.
3. Create the required Controller, Settings, Analytics, Records, Dialog, Shortcut, and Overlay frames.
4. Use realistic sample data.
5. Create dark and light modes.
6. Create compact and minimum-width states.
7. Preserve feature completeness.

### Phase 4: Prototype

1. Connect the required flows.
2. Use overlays, component state changes, and navigation transitions sparingly and clearly.
3. Add annotations where actual application behavior cannot be represented faithfully in a static prototype.

### Phase 5: QA and refinement

For every major page:

1. Inspect metadata and hierarchy.
2. Capture screenshots.
3. Check alignment, clipping, overflow, text truncation, spacing, state clarity, and contrast.
4. Compare common components across screens for consistency.
5. Fix all obvious visual or structural defects.
6. Verify that screens use component instances and semantic variables.
7. Verify both Light and Dark modes.
8. Verify minimum-size frames.
9. Remove temporary nodes, placeholders, and shimmer states.
10. Confirm that no old content was overwritten.

Do not claim completion without screenshot-based visual QA.

## State ledger

Create or update this local project file:

`design-system-state-stopwatch-professional.json`

Track at least:

- Run ID.
- Figma file key and URL.
- Current phase and step.
- Page IDs.
- Variable collection IDs.
- Style IDs.
- Component and component-set IDs.
- Screen frame IDs.
- Completed steps.
- Pending validations.
- Actual blockers.
- Design decisions that materially affect implementation.

Use the ledger to avoid duplicate pages or components and to support safe continuation.

Do not store secrets, tokens, or authentication data in the ledger.

## Handoff requirements

Create the `09 - Prototype Flows and Handoff` page with:

- Final screen inventory.
- Component inventory.
- Token table.
- Interaction notes.
- Accessibility notes.
- Window resizing rules.
- Empty, loading, disabled, and error-state rules.
- Keyboard behavior.
- Mapping from Figma sections to:
  - `App.xaml`
  - Theme resource dictionaries.
  - `ControllerWindow.xaml`
  - `OverlayWindow.xaml`
  - `ProjectDashboardWindow.xaml`
  - `ProjectRecordsWindow.xaml`
  - Dialog XAML files.
- A recommended implementation sequence.
- A list of optional future enhancements separated from required v1 changes.
- Before and after comparison using the existing local screenshots and final Figma screenshots.

## Quality bar

The redesign is successful only when:

- The active timer is immediately obvious.
- Running timers remain distinct from the active timer.
- Primary timer actions are visible without scrolling.
- Multiple timers are easy to scan and switch.
- Infrequent settings no longer dominate the main controller.
- Countdown controls appear only in relevant contexts.
- Overlay customization has a useful live preview.
- Analytics are clear, labeled, and interpretable without relying only on color.
- Records are faster to scan than the current stacked presentation.
- Dialogs use one consistent structure.
- Floating overlays stay lightweight and readable.
- Light and Dark modes feel like one design system.
- Minimum-size layouts remain usable.
- Keyboard focus and shortcut visibility are intentional.
- Final screens use reusable components and semantic variables.
- Every important current feature has a visible home in the redesign.
- Existing Figma content remains intact.
- The final result looks like a production-ready desktop application, not an AI-generated mood board.

## Failure handling

If a Figma MCP call fails:

1. Read the exact error.
2. Do not repeat the same call blindly.
3. Correct the script or tool arguments.
4. Continue from the state ledger.
5. Keep successful work intact.

If the target Figma file is inaccessible, report the exact access or permission blocker. Do not create or switch to another file unless explicitly instructed.

If Figma authentication, permissions, or tool-call limits fully block writes:

- Report the exact blocker.
- Record it in the state ledger.
- Complete the local project audit and implementation-aware design specification.
- Do not present local SVG exports as equivalent to a completed Figma design.
- Do not overwrite or delete prior design assets.

## Final response format

At completion, report:

1. Figma file URL and file key.
2. Pages created or updated.
3. Variables, styles, and components created.
4. High-fidelity frames completed.
5. Prototype flows completed.
6. Screenshots and QA checks performed.
7. Key UX decisions.
8. WPF implementation mapping.
9. Optional future enhancements.
10. Remaining blockers or risks, if any.
11. State-ledger path.
12. Confirmation that existing Figma content and production code were preserved.
