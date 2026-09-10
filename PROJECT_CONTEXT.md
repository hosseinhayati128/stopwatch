# Stopwatch Overlay: Project Context & Architecture Reference

> **Notice:** This document is the durable project map and engineering onboarding reference for the local **Stopwatch Overlay** repository. It was compiled from direct inspection of first-party source code, project configuration, XAML theme definitions, and unit/integration tests. Build and test commands documented herein were verified from configuration files and scripts, not executed during onboarding.

---

## 1. Product Purpose

**Stopwatch Overlay** is a Windows desktop timing and project time-tracking application designed for deep work, live streaming, screen recording, and presentations.

### Key Capabilities
- **Four Timing Modes**:
  - **Stopwatch**: Counts up with start, pause/resume, lap recording, and reset.
  - **Clock**: Real-time wall-clock display with optional blinking colon.
  - **Countdown**: Fixed duration countdown (with smart natural-language input support) or wall-clock target ("until 17:00") countdown; continues counting into negative if overshot.
  - **Timecode**: Frame-accurate timecode counter (`HH:MM:SS:FF`) at configurable frame rates (default 30 fps).
- **Independent Multi-Timer Workspace**: Multiple logical timers can run concurrently. Exactly one timer is designated as `Active` and serves as the sole target for global hotkey commands.
- **Floating Overlays**: Always-on-top, transparent, click-through-capable HUD windows with outlined text rendering for legibility against any background. Individual overlays can be shown per timer or combined into a single shared overlay that reflects the active timer.
- **Detached Hover Controls**: Hovering over an overlay displays a floating `Popup` toolbar containing Close, Pause/Resume, and Reset buttons. These do not affect the measured size or layout of the timing text.
- **Automatic & Manual Project Tracking**: Running named timers automatically record UTC work intervals in a crash-safe local database. Completed records can be viewed, filtered, manually created, edited, and deleted via an analytics dashboard.
- **Independent Theme System**: Five application panel themes (Midnight, Daylight, Pixel Deck Night, Pixel Deck Day, Acanthus) and nine floating-clock themes (including three dark Acanthus variants and a "Follow Application Theme" option) can be selected independently without cross-contamination.
- **Seamless Tiled Backgrounds & Light Ring**: Nine built-in patterns or validated user-imported images can be composited over panel backgrounds and clock surfaces. An optional screen-edge illumination light ring supports webcam lighting.

---

## 2. Current Implementation Status

| Feature Area | Status | Verified Evidence & Constraints |
|---|---|---|
| **Multi-Timer Engine** | **Implemented** | [`TimerSessionManager`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/TimerSessionManager.cs) manages ordered timers, monotonic numbering, cycling, and activation without WPF dependencies. |
| **Workspace Persistence** | **Implemented** | [`TimerWorkspaceStore`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/TimerWorkspaceStore.cs) persists versioned JSON snapshots (`workspace.json` & `workspace.json.bak`) with offline time compensation and lock retries. |
| **Independent Overlay Themes** | **Implemented** | [`OverlayThemeCatalog`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayThemeCatalog.cs) & [`OverlayThemeManager`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayThemeManager.cs) apply local `ResourceDictionary` scopes to overlay windows and preview surfaces. Panel themes and overlay themes are fully decoupled. |
| **Combined vs Separate Overlay** | **Implemented** | [`OverlayPresentationPolicy`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayPresentationPolicy.cs) & Win+F12 presentation toggle dynamically display the active timer in a shared shell without altering underlying timer run states. |
| **Project Time History & Analytics** | **Implemented** | [`ProjectTimeStore`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectTimeStore.cs), [`ProjectTimeHistory`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectTimeHistory.cs), and [`ProjectDashboardAnalytics`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectDashboardAnalytics.cs) manage UTC work sessions, half-open date clipping, 53-week heatmap, and CRUD operations. |
| **Crash Logging & Recovery** | **Implemented** | [`CrashLogger`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/CrashLogger.cs) handles unhandled exceptions with path redaction and 10-log retention under `%LOCALAPPDATA%\StopwatchOverlay\Logs`. Startup recovery guards prevent overwriting valid data if an existing store is locked or transiently unreadable. |
| **Screen Capture Exclusion** | **Implemented** | Win32 `SetWindowDisplayAffinity` (`WDA_EXCLUDEFROMCAPTURE`) is hooked for overlays and light ring windows. |

---

## 3. Workspace Map

```
stopwatch-main/
├── StopwatchOverlay.sln                     # Visual Studio / .NET solution
├── DEVELOPERS.md                           # Developer instructions and architecture notes
├── README.md                               # End-user documentation and screenshots
├── specs/                                  # Historical prompts, redesign briefs, and onboarding specs
│   ├── ANTIGRAVITY_PROJECT_ONBOARDING.md   # Authoritative onboarding spec for this pass
│   ├── IMPLEMENT_FLOATING_CLOCK_THEME_SYSTEM.md # Historical specification for independent overlay themes
│   └── FIX_CURRENT_APP_STABILITY_AND_SETTINGS.md# Historical stability repair instructions
├── .codex-stability-repair/                # Historical stability investigation logs, patches, and reports
│   └── repair-log.md                       # Comprehensive log of root-cause investigations and fixes
├── StopwatchOverlay/                       # Main WPF application (.NET 10 Windows)
│   ├── StopwatchOverlay.csproj             # Project file (UseWPF, UseWindowsForms, single-file publish config)
│   ├── App.xaml / App.xaml.cs              # Application entry, single-instance mutex/event, crash hooks
│   ├── ControllerWindow.xaml / .cs         # Main control panel, timer ticking, hotkey handling, window management
│   ├── OverlayWindow.xaml / .cs            # Transparent floating clock overlay with hover popup
│   ├── SettingsWindow.xaml / .cs           # Categorized preferences editor, live preview, interaction throttling
│   ├── LightRingWindow.xaml / .cs          # Non-activating, click-through border illumination window
│   ├── ProjectDashboardWindow.xaml / .cs   # Analytics dashboard, charts, timeline, inline record editor
│   ├── ProjectRecordEditorWindow.xaml / .cs# Local-time CRUD modal dialog for historical project intervals
│   ├── ProjectRecordDeleteWindow.xaml / .cs# Deletion confirmation dialog for closed project intervals
│   ├── TimerNameWindow.xaml / .cs          # Project chooser / creator dialog for new or active timers
│   ├── ShortcutsWindow.xaml / .cs          # Global hotkey binding and configuration editor
│   ├── ConfirmationDialogWindow.xaml / .cs # Generic styled modal confirmation prompt
│   ├── TimerSession.cs                     # Runtime model for one logical timer, ResumableStopwatch
│   ├── TimerSessionManager.cs              # Collection and active selection management for TimerSessions
│   ├── TimerWorkspaceStore.cs              # Atomic persistence and backup rotation for workspace.json
│   ├── ProjectTimeHistory.cs               # In-memory registry of projects, open/closed work intervals
│   ├── ProjectTimeStore.cs                 # Atomic persistence and backup rotation for project-history.json
│   ├── ProjectDashboardAnalytics.cs        # Time-zone-aware clipping, date bounds, and 53-week heatmap math
│   ├── AppSettings.cs                      # Settings model (JSON) and SettingsStore persistence
│   ├── AppThemeManager.cs                  # Application-wide theme catalog and palette switcher
│   ├── OverlayThemeCatalog.cs              # Nine floating-clock theme definitions and normalization
│   ├── OverlayThemeManager.cs              # Window-local ResourceDictionary resolver for overlay themes
│   ├── AppBackgroundManager.cs             # Built-in presets, custom managed background import/scaling/tiling
│   ├── CountdownParser.cs                  # Natural language duration and clock-target parser
│   ├── CrashLogger.cs                      # Exception logging, UI action breadcrumbs, private path redaction
│   ├── StartupRegistration.cs              # Windows registry Run key integration (HKCU)
│   ├── SettingsChange.cs                   # SettingsChangeKind bitmask flags and coalescing policy
│   ├── ControllerLayoutPolicy.cs           # Breakpoint width logic for compact vs standard layout
│   ├── OverlayPresentationPolicy.cs        # Pure presentation helpers (clamping opacity, combined selection)
│   ├── Assets/                             # Bundled images, background tiles, SVG ornaments, Cascadia/Inter fonts
│   └── Themes/                             # XAML resource dictionaries for panels and overlays
│       ├── Midnight.xaml                   # Neutral dark application theme
│       ├── Daylight.xaml                   # Neutral light application theme
│       ├── PixelDeck.xaml                  # Cyberpunk dark application theme
│       ├── PixelDeckDay.xaml               # Cyberpunk light application theme
│       ├── Acanthus.xaml                   # Classical botanical light application theme
│       ├── AcanthusStyles.xaml / Ornaments # Shared Acanthus vectors, buttons, and control styles
│       ├── AcanthusVisual.cs               # Attached properties for Acanthus scope and dynamic overrides
│       └── Overlay/                        # Independent floating-clock theme dictionaries
│           ├── OverlayDefaults.xaml        # Common base metrics, paddings, and font sizes
│           ├── OverlayStyles.xaml          # Close, pause/resume, and reset button templates
│           ├── OverlayOrnaments.xaml       # Vectors for corner, crest, and leaf ornaments
│           ├── MidnightOverlay.xaml        # Midnight clock styling
│           ├── DaylightOverlay.xaml        # Daylight clock styling
│           ├── PixelDeckNightOverlay.xaml  # Pixel Deck Night clock styling
│           ├── PixelDeckDayOverlay.xaml    # Pixel Deck Day clock styling
│           ├── AcanthusLightOverlay.xaml   # Acanthus Light clock styling
│           ├── AcanthusDarkElegantOliveOverlay.xaml # Acanthus Dark: Elegant Olive clock styling
│           ├── AcanthusDarkGoldCrestOverlay.xaml    # Acanthus Dark: Gold Crest clock styling
│           └── AcanthusDarkMinimalBotanicalOverlay.xaml # Acanthus Dark: Minimal Botanical clock styling
├── StopwatchOverlay.Tests/                 # Unit and integration test suite (xUnit)
│   ├── StopwatchOverlay.Tests.csproj       # Test project file
│   ├── AppSettingsStoreTests.cs            # Tests for settings persistence, fallback, corruption handling
│   ├── TimerSessionManagerTests.cs         # Tests for timer creation, ordering, cycling, closing
│   ├── TimerWorkspaceStoreTests.cs         # Tests for atomic workspace save/load, offline time, backup recovery
│   ├── ProjectTimeStoreTests.cs            # Tests for project tracking, interval reconciliation, manual CRUD
│   ├── ProjectDashboardAnalyticsTests.cs   # Tests for timezone clipping, half-open ranges, heatmap generation
│   ├── OverlayThemeSettingsTests.cs        # Tests for 45 independent theme combinations & migration
│   ├── OverlayThemeResourcesTests.cs       # Tests for overlay palette resource contracts & Figma values
│   ├── OverlayWindowThemeTests.cs          # Real STA WPF tests verifying in-place overlay theme switching
│   ├── ThemeHoverResourcesTests.cs         # Tests for hover colors, opacities, and button templates
│   ├── AcanthusThemeResourcesTests.cs      # Tests for Acanthus palette tokens and vector resources
│   ├── CountdownParserTests.cs             # Tests for natural language countdown parsing
│   ├── ShortcutSettingsTests.cs            # Tests for shortcut formatting and conflict validation
│   ├── ControllerLayoutPolicyTests.cs      # Tests for window layout policy
│   └── OverlayPresentationPolicyTests.cs  # Tests for overlay presentation decisions
└── tools/ReadmeScreenshots/                # Synthetic data renderer for generating README screenshot assets
```

---

## 4. Architecture and State Ownership

### Subsystem Boundaries

| Subsystem | State Owned | State Displayed | Creator / Lifecycle | Persistence & Constraints |
|---|---|---|---|---|
| **[`TimerSessionManager`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/TimerSessionManager.cs)** | Ordered collection of `TimerSession`s, monotonic `_nextNumber`, active `TimerSession?`. | None (pure domain model). | Instantiated by [`ControllerWindow`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ControllerWindow.xaml.cs). Pure C#, no WPF dependencies. | Cleanly serializable to `workspace.json`. Single-threaded UI domain. |
| **[`TimerSession`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/TimerSession.cs)** | `Id`, `Number`, `Name`, `Mode`, `ResumableStopwatch`, `CountdownDuration/Remaining`, `LapTimes`, overlay visibility and coordinates. | Formatted elapsed/countdown strings. | Created by `TimerSessionManager.Create()`. | Holds runtime state. Negative elapsed times forbidden. |
| **[`ControllerWindow`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ControllerWindow.xaml.cs)** | Screen selection, presentation modes (`_combinedOverlayMode`), dialog references, UI dirty flags, recovery status. | Active timer state, lap history, status messages. | Created on startup via `App.xaml` `StartupUri`. Runs until tray exit or process termination. | Ticks 50ms dispatcher timer, periodic 1s save timer, registers Win32 hotkeys. |
| **[`OverlayWindow`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayWindow.xaml.cs)** | Window-local HWND, drag state, hover popup visibility/timer, local theme dictionary. | Active/assigned timer time and project name. | Created and destroyed by `ControllerWindow`. Supports multiple monitor replicas. | `Topmost=True`, `ShowActivated=False`, `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`, optional `WS_EX_TRANSPARENT`. |
| **[`SettingsStore`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/AppSettings.cs)** | None (static service). Loads/saves [`AppSettings`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/AppSettings.cs). | N/A | Static methods called by `ControllerWindow` and `SettingsWindow`. | Writes `%APPDATA%\StopwatchOverlay\settings.json` with `.bak` fallback and corruption backup. |
| **[`AppThemeManager`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/AppThemeManager.cs)** | `CurrentTheme` string. | Updates `Application.Current.Resources`. | Static service. | In-memory resource mutation. Matches system `ThemeMode.Light/Dark`. |
| **[`OverlayThemeManager`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayThemeManager.cs)** | Weak reference cache of applied local palettes per `FrameworkElement`. | Merges palette into target element's `MergedDictionaries`. | Static service called by `OverlayWindow` and `SettingsWindow` preview. | **Never writes `Application.Resources`**; strictly window/preview-local. |
| **[`AppBackgroundManager`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/AppBackgroundManager.cs)** | Decoded background cache, active tiled brush, custom image catalog. | Generates brushes for application window and overlay surface. | Static service. | Custom images validated and copied to `%LOCALAPPDATA%\StopwatchOverlay\Backgrounds`. |
| **[`ProjectTimeHistory`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectTimeHistory.cs)** | Thread-safe lists of `ProjectEntry` and `WorkIntervalEntry`. | Generates detached, immutable `ProjectHistoryView` snapshots. | Created/owned by `ControllerWindow`. | Synchronized via `lock (_gate)`. Canonical project naming (case-insensitive key, first casing preserved). |
| **[`ProjectTimeStore`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectTimeStore.cs)** | File paths and recovery status flags (`NeedsPrimaryRepair`). | Serializes/deserializes `ProjectHistoryDocument`. | Owned by `ControllerWindow`. | Writes `%APPDATA%\StopwatchOverlay\project-history.json` with `.bak` and atomic swap. |
| **[`ProjectDashboardWindow`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectDashboardWindow.xaml.cs)** | Selected range, date, project filter, pagination index. | Displays charts, timelines, 53-week heatmap, and closed records. | Opened on Win+F11 or menu click. Mutates records via controller delegate callbacks. | Does not write to disk directly; delegates additions, edits, and deletions to `ControllerWindow`. |

---

## 5. Main End-to-End Workflows

### 5.1 Timer Lifecycle
1. **Creation**: User presses Win+F2 or clicks "New Timer".
   - `ControllerWindow.NewTimerButton_Click` opens modal `TimerNameWindow(isCreatingTimer: true)`.
   - Neutral selection creates an unnamed timer; entering or choosing a name assigns the project.
   - `TimerSessionManager.Create()` allocates the next sequential integer, adds the session to its ordered list, and sets it as `Active`.
   - If a project was chosen and the timer starts, `ProjectTimeHistory.StartTracking` opens a work interval with `StartUtc = DateTime.UtcNow`.
   - Overlay windows are constructed per selected display via `CreateOverlayForScreen` (or refreshed in combined mode).
   - `CheckpointState()` triggers atomic workspace persistence.
2. **Start / Pause / Resume**: User presses Win+F5, clicks Controller Start/Stop, or clicks Overlay toolbar Pause/Resume.
   - Action targets **only** `TimerSessionManager.Active`.
   - If running, `session.Stopwatch.Stop()` stops monotonic accumulation, `session.IsRunning = false`, and if named, `ProjectTimeHistory.StopTracking(session.Id, DateTime.UtcNow)` closes the open interval with `EndUtc = DateTime.UtcNow`.
   - If paused, `session.Stopwatch.Start()` resumes accumulation, `session.IsRunning = true`, and if named, `ProjectTimeHistory.StartTracking` opens a new interval.
3. **Recording a Lap**: User presses Win+F8 or clicks Lap.
   - Formatted elapsed time string is inserted into `session.LapTimes`. `session.LapCount++`.
4. **Project Switch (Win+F10)**:
   - `TimerNameWindow` returns the selected project.
   - If timer has accumulated time and was assigned to an existing project, old interval closes at transition instant $T$, session resets elapsed time to zero (`ResetForProjectSwitch`), and a new interval starts at $T$ with the new project name. Running/paused state is preserved.
5. **Closing a Timer**: User presses Win+F4 or clicks Overlay Close button.
   - If running, work interval is closed at current UTC instant.
   - `TimerSessionManager.Close(session)` removes it. If active, its next neighbor is activated. Replicas are closed via `CloseTimerOverlays`.

### 5.2 Settings Interaction & Slider Throttling
1. **Input Initiation**: User drags an Appearance slider (e.g. `TextSizeSlider`, `BackgroundOpacitySlider`).
   - `WireSlider` detects `PreviewMouseLeftButtonDown` or slider keys and invokes `SettingsInteractionStarted`.
   - `ControllerWindow._settingsInteractionInProgress = true`. This suspends heavy background tasks and prevents periodic checkpoint timer from writing mid-drag.
2. **Value Changes**: Each `Slider.ValueChanged` calls `CommitControls(changeKind)`.
   - `_settingsWindow.CommitControls` updates in-memory `AppSettings`.
   - Dependent labels (`TextSizeValueText`) update immediately.
   - `_previewTimer` (33 ms debouncer) coalesces calls to `UpdatePreviewSafely()`.
   - `SettingsChanged?.Invoke(changeKind)` notifies `ControllerWindow`.
3. **Controller Coalescing**:
   - Continuous changes (geometry, appearance, background strength) are buffered into `_pendingDedicatedSettingsChanges`.
   - `_dedicatedSettingsApplyTimer` (40 ms) coalesces updates to live overlays. Overlays update via `ApplyOverlaySettings` (reusing existing windows, adjusting fonts, outlines, and surface brushes without window recreation).
4. **Interaction Completion**: User releases mouse (`PreviewMouseLeftButtonUp` or `LostMouseCapture`).
   - `SettingsInteractionCompleted` triggers `QueueSettingsCompletion()`.
   - Dispatches at `DispatcherPriority.ContextIdle` to flush pending changes, apply background strength, and invoke `CheckpointStateNow()`.

### 5.3 Theme Selection
1. **Application Panel Theme Change**:
   - User selects theme in `ThemeCombo` (e.g. `Daylight`).
   - Commits `SettingsChangeKind.Theme`.
   - Controller calls `AppThemeManager.Apply(theme)`:
     - Idempotent check ensures no-op if same theme requested.
     - Loads palette XAML (e.g. `Themes/Daylight.xaml`).
     - Clones/replaces brushes in `Application.Current.Resources`.
     - Updates native `Application.Current.ThemeMode` to `Light` or `Dark`.
   - If `OverlayTheme` is set to `Follow Application Theme`, overlay geometry and palettes are refreshed to match the resolved counterpart.
2. **Floating-Clock Theme Change**:
   - User selects theme in `OverlayThemeCombo` (e.g. `Acanthus Dark Elegant Olive`).
   - Commits `SettingsChangeKind.OverlayTheme`.
   - Does **NOT** call `AppThemeManager` and does **NOT** touch `Application.Current.Resources`.
   - Controller iterates `_overlayInstances` and `_combinedOverlayInstances`, calling `overlay.ApplyTheme(overlayTheme, applicationTheme)`:
     - Calls `OverlayThemeManager.Apply(overlay, overlayTheme, applicationTheme)` and `OverlayThemeManager.Apply(ActionPopupRoot, ...)`.
     - Resolves palette from `Themes/Overlay/{File}.xaml`.
     - Merges palette directly into window-local `target.Resources.MergedDictionaries`.
   - Settings window preview calls `OverlayThemeManager.Apply(PreviewThemeScope, ...)` to update preview in-place.

### 5.4 Floating Overlays (Separate vs Combined)
1. **Separate View (Default)**:
   - Each `TimerSession` has its own set of `OverlayWindow` instances (one per selected monitor screen).
   - Clicking an overlay invokes `overlay.ActivationRequested`, calling `ControllerWindow.ActivateTimer(timer)`.
   - Dragging calls Win32 `DragMove()` and saves per-screen coordinates in `timer.CustomPositionsByScreen`.
2. **Combined View (Win+F12 Toggle)**:
   - `_combinedOverlayMode` switches to true.
   - All individual timer overlays are closed with positions preserved on the session objects.
   - A single shared `OverlayWindow` is created per screen (`_combinedOverlayInstances`).
   - `RefreshCombinedOverlayState()` binds the shared overlay to display `_activeTimer`'s formatted time, project name, and running state.
   - Cycling active timer with Win+F3 immediately updates the shared overlay to display the newly activated session without interrupting other running sessions.

### 5.5 Records and Analytics
1. **Tracking**: When a named timer is running, `ProjectTimeHistory` holds an open `WorkIntervalEntry` (`EndUtc == null`).
2. **Dashboard Query**:
   - `ProjectDashboardWindow` calls `_historyProvider()` which invokes `_projectHistory.CreateView(DateTime.UtcNow)`.
   - `ProjectHistoryView` produces an immutable, detached snapshot of all projects and intervals.
   - `ProjectDashboardAnalytics.CreateRange` computes the half-open UTC range `[StartUtc, EndUtc)` for the selected local calendar day or range (7 days, 30 days, All time).
   - `ProjectDashboardAnalytics.Clip` clips intervals to the window bounds, accurately splitting midnight crossovers across local days.
3. **Manual Mutations**:
   - User clicks "Add record", edits an existing closed record, or deletes a record.
   - Dialog collects inputs in local time, validates against future dates, positive duration, and Daylight Saving gaps, then converts to UTC.
   - Mutation delegates back to `ControllerWindow` (`AddManualProjectRecord`, `UpdateProjectRecord`, `DeleteProjectRecord`).
   - `ProjectTimeHistory` performs mutation under its internal lock, validates that no interval within the same timer session overlaps, marks `_projectHistoryDirty = true`, and calls `CheckpointStateNow()`.

### 5.6 Startup, Shutdown, and Recovery
1. **Startup**:
   - `App.OnStartup`: Acquires single-instance mutex `Local\StopwatchOverlay.SingleInstance`. If another instance exists, signals `Local\StopwatchOverlay.ShowExisting` and exits.
   - `CrashLogger` hooks unhandled exception handlers.
   - `ControllerWindow` loads `AppSettings` via `SettingsStore.Load()`.
   - Applies panel theme and background brush before UI rendering.
   - `TimerWorkspaceStore.TryLoad`: Reads `workspace.json`. If corrupt or missing, tries `workspace.json.bak`. Computes `offlineTime = DateTime.UtcNow - snapshot.SavedAtUtc`. If a timer was running when the app closed, its elapsed time is incremented by `offlineTime`.
   - `ProjectTimeStore.TryLoad`: Reads `project-history.json` and reconciles open intervals with running named timers.
2. **Shutdown**:
   - Closing Controller with window X minimizes to notification area (tray).
   - Tray menu "Exit" triggers `ExitApplication()`.
   - `FlushPendingSettingsBeforeExit()` flushes uncommitted settings.
   - `CheckpointStateNow()` writes workspace and project history atomically.
   - Native global hotkeys are unregistered via `UnregisterHotKey`.
   - Single-instance mutex is released.

---

## 6. Theme System Architecture

### Palettes and Independence Status
**Verified Status:** The application panel themes and floating-clock overlay themes are **completely independent**.

```
                       ┌─────────────────────────────────────────┐
                       │               AppSettings               │
                       ├────────────────────┬────────────────────┤
                       │  ThemeMode (Panel) │    OverlayTheme    │
                       └─────────┬──────────┴──────────┬─────────┘
                                 │                     │
                    ┌────────────▼─────────┐ ┌─────────▼──────────────┐
                    │   AppThemeManager    │ │  OverlayThemeManager   │
                    └────────────┬─────────┘ └─────────┬──────────────┘
                                 │                     │
                    Modifies Global Scope   Modifies Element Scope Only
                                 │                     │
                    ┌────────────▼─────────┐ ┌─────────▼──────────────┐
                    │ Application.Resources│ │ OverlayWindow.Resources│
                    │   (Panels, Dialogs)  │ │   (Floating Clocks)    │
                    └──────────────────────┘ └────────────────────────┘
```

- **Application Themes**: `Midnight`, `Daylight`, `Pixel Deck Night`, `Pixel Deck Day`, `Acanthus`.
  - Stored in `AppSettings.ThemeMode` (persisted in JSON) with `[JsonIgnore] ApplicationTheme` property alias for compatibility.
  - Applied via `AppThemeManager.Apply()`. Replaces brushes in `Application.Current.Resources`.
- **Floating Clock Themes**: `Follow Application Theme`, `Midnight`, `Daylight`, `Pixel Deck Night`, `Pixel Deck Day`, `Acanthus Light`, `Acanthus Dark Elegant Olive`, `Acanthus Dark Gold Crest`, `Acanthus Dark Minimal Botanical`.
  - Stored in `AppSettings.OverlayTheme`.
  - Resolved via `OverlayThemeCatalog.Resolve(overlayTheme, applicationTheme)`.
  - Applied via `OverlayThemeManager.Apply(target, ...)`. Replaces local dictionary in `target.Resources.MergedDictionaries`.
- **Resource Precedence**:
  1. `OverlayDefaults.xaml`: Baseline metrics (min widths, margins, font sizes).
  2. Concrete Overlay XAML (e.g. `AcanthusDarkGoldCrestOverlay.xaml`): Theme-specific colors, borders, brushes, corner radii.
  3. Window settings overrides: `_textColor`, `_borderColor`, `_fontSize`, `_borderWidth`, `_backgroundOpacity`. (Note: text and border colors honor user overrides unless "Theme default" is selected).

---

## 7. Data Storage, Schemas, and Recovery

All persistent files are stored under Windows user profile paths:

### 1. Settings
- **Path**: `%APPDATA%\StopwatchOverlay\settings.json` (and `.bak`)
- **Format**: JSON (`AppSettings`)
- **Key Fields**: `ThemeMode`, `OverlayTheme`, `PanelBackgroundId`, `PanelBackgroundStrength`, `TextColor`, `BorderColor`, `FontFamily`, `TextSize`, `BackgroundOpacity`, `Position`, `CustomPositions`, `LightRing*`, `Shortcuts`.

### 2. Timer Workspace
- **Path**: `%APPDATA%\StopwatchOverlay\workspace.json` (and `.bak`)
- **Format**: JSON (`TimerWorkspaceSnapshot`, Version 1)
- **Key Fields**: `SavedAtUtc`, `ActiveTimerId`, `NextNumber`, `CombinedOverlayMode`, `CombinedPositionsByScreen`, `Timers` array. Each timer contains `Id`, `Number`, `Name`, `IsRunning`, `Elapsed`, `Mode`, `Countdown*`, `LapTimes`, custom screen coordinates.
- **Write Mechanism**: Atomically writes to `.tmp.{guid}`, then calls `File.Replace` with `.bak` generation.
- **Recovery Protection**: If primary file is locked/busy during startup, it is flagged as `Unavailable`. The controller enters read-only recovery mode, retrying every 2 seconds until readable, never overwriting unavailable files with blank data.

### 3. Project History
- **Path**: `%APPDATA%\StopwatchOverlay\project-history.json` (and `.bak`)
- **Format**: JSON (`ProjectHistoryDocument`, Version 1)
- **Key Fields**: `SavedAtUtc`, `Projects` (`Key`, `Name`), `Intervals` (`Id`, `TimerSessionId`, `ProjectKey`, `ProjectName`, `StartUtc`, `EndUtc`).
- **Reconciliation**: Startup reconciles open intervals (`EndUtc == null`) against restored running timers. Missing intervals for running timers are created; orphaned intervals for stopped timers are cleanly closed.

### 4. Managed Custom Backgrounds
- **Path**: `%LOCALAPPDATA%\StopwatchOverlay\Backgrounds\`
- **Files**: `custom-{id}.jpg/.png/.bmp`
- **Behavior**: Images are imported, dimension-checked (max 8,192 px / 40 MP), and copied into managed storage. Settings refer only to the generated ID and leaf filename.

### 5. Crash Logs
- **Path**: `%LOCALAPPDATA%\StopwatchOverlay\Logs\`
- **Files**: `crash-{timestamp}-p{pid}-t{thread}-{guid}.log`
- **Behavior**: Maximum 10 newest logs retained. Machine and user paths are sanitized/redacted via regex.

---

## 8. Lifecycle, Threading, and Performance

- **Threading Model**: WPF UI thread is the single source of truth for UI state, timer ticks, and controller state. Background tasks (e.g. startup load retries, single-instance listener) dispatch back to UI thread via `Dispatcher.BeginInvoke`.
- **Active Dispatcher Timers**:
  - `_timer` (50 ms): Updates active timing calculations, formatted displays, and overlay text.
  - `_blinkTimer` (500 ms): Alternates colon blink and REC indicator states.
  - `_stateSaveTimer` (1 s): Flushes dirty state to disk and handles background recovery retries.
  - `_backgroundApplyTimer` (140 ms): Debounces expensive background texture regeneration during slider adjustments.
  - `_dedicatedSettingsApplyTimer` (40 ms): Coalesces overlay visual property changes during continuous slider interactions.
  - `_previewTimer` in SettingsWindow (33 ms): Debounces live preview rendering (~30 fps).
- **Cleanup & Memory Leaks**:
  - Event subscriptions between `SettingsWindow` and `ControllerWindow` are explicitly unwired on window close in `SettingsWindowClosed`.
  - `OverlayThemeManager` uses `ConditionalWeakTable<FrameworkElement, AppliedPalette>` to prevent leaking references to closed windows.
  - Monitored image decoders use `BitmapCacheOption.OnLoad` and freeze bitmap sources.

---

## 9. Build, Run, and Test Guide

> **Note:** The following commands were verified from the repository configuration and script files. They were **not** executed during this onboarding pass.

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`net10.0-windows`)
- Windows 10 or 11 (Win32 API interop: User32, Shcore)

### Build Commands
```powershell
# Restore dependencies and build solution in Debug
dotnet build StopwatchOverlay.sln

# Build solution in Release configuration
dotnet build StopwatchOverlay.sln -c Release
```

### Run Tests
```powershell
# Run the entire test suite (xUnit)
dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj

# Run tests with verbose output
dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj --logger "console;verbosity=normal"
```

### Publish Single-File Executable
```powershell
# Framework-dependent single-file publish (requires installed .NET 10 Desktop Runtime)
dotnet publish StopwatchOverlay/StopwatchOverlay.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o release-standard
```

---

## 10. Change Guide for Future Work

When making future changes, start in the following locations:

| Type of Change | Primary Files to Touch | Key Considerations & Invariants |
|---|---|---|
| **Timer Logic / Modes** | [`TimerSession.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/TimerSession.cs), [`TimerSessionManager.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/TimerSessionManager.cs), [`ControllerWindow.xaml.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ControllerWindow.xaml.cs) | Keep `TimerSessionManager` free of WPF dependencies. Ensure `TimerWorkspaceSnapshot` handles any new fields with backward-compatible defaults. |
| **Floating Clock Overlay** | [`OverlayWindow.xaml`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayWindow.xaml) / [`.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayWindow.xaml.cs), [`OverlayPresentationPolicy.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayPresentationPolicy.cs) | Do not bake opacity into text/border/toolbar. Keep action controls inside the detached `ActionPopup` so timer text measurement never fluctuates on hover. |
| **Theme / Appearance** | [`Themes/Overlay/`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/Themes/Overlay/), [`OverlayThemeCatalog.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayThemeCatalog.cs), [`OverlayThemeManager.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/OverlayThemeManager.cs) | Never inject overlay-specific keys into `Application.Current.Resources`. All overlay themes must satisfy the complete resource contract in `OverlayDefaults.xaml`. |
| **Settings UI / Inputs** | [`SettingsWindow.xaml`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/SettingsWindow.xaml) / [`.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/SettingsWindow.xaml.cs), [`SettingsChange.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/SettingsChange.cs) | Classify any new setting in `SettingsChangeKind`. If continuous (slider), wire to `WireSlider` with interaction guards and coalescing to prevent UI thread lockups. |
| **Project Tracking / Analytics** | [`ProjectTimeHistory.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectTimeHistory.cs), [`ProjectTimeStore.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectTimeStore.cs), [`ProjectDashboardAnalytics.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ProjectDashboardAnalytics.cs) | Maintain UTC persistence on disk and half-open `[StartUtc, EndUtc)` boundaries. Never allow overlapping intervals on the same timer session. Group by local calendar date for display. |
| **Crash / Recovery Issues** | [`CrashLogger.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/CrashLogger.cs), [`App.xaml.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/App.xaml.cs), `TimerWorkspaceStore.cs` | Inspect `%LOCALAPPDATA%\StopwatchOverlay\Logs`. Look for transient file-sharing violations or recursive style evaluations. Verify atomic write-through and `.bak` preservation. |

---

## 11. Do-Not-Break Invariants

1. **Single Command Target**: Hotkeys (Win+F5–Win+F10) and toolbar clicks must **only** affect `TimerSessionManager.Active`, never broadcast across all running timers.
2. **Presentation Combining**: Win+F12 changes presentation only; it must never pause, reset, or alter project tracking for non-active timers.
3. **Decoupled Theme Scopes**: Overlay theme changes must never mutate `Application.Current.Resources` or alter panel themes, and vice versa.
4. **Opacity Isolation**: Overlay background opacity must only apply to `OverlayBackgroundSurface`; timing digits, project name, outline, borders, and popup controls must remain 100% opaque.
5. **Atomic File Persistence**: Never write directly to primary JSON files. Always write to a temporary file with `FileOptions.WriteThrough` and swap atomically via `File.Replace` preserving `.bak`.
6. **Pristine Data Protection**: Never treat an inaccessible or locked file as an empty workspace. Enter protected recovery mode and retry until readable.
7. **Single-Instance Enforcement**: Only one instance of the app may run at a time; secondary launches must signal the existing instance and exit.
8. **Tray Residency**: Closing the main window (X) minimizes to the system notification area; true exit occurs only via Tray menu Exit or explicit app shutdown.
9. **Native Input & Capture Affinity**: Value sliders must retain native WPF slider templates with integer steps. `LightRingWindow` must strictly remain non-activating and click-through (`WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`).

---

## 12. Known Risks & Open Observations

1. **WPF `Application.ThemeMode` Re-entrancy Risk**:
   - *Code pointer*: [`AppThemeManager.cs:L81-L90`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/AppThemeManager.cs#L81-L90)
   - *Detail*: In .NET 9/10, setting `Application.Current.ThemeMode` can cause recursive style evaluations on `ScrollViewer`/`Slider` controls if changed while a `Thumb` is captured. The codebase protects this with an idempotency check (`_currentTheme == theme`) and deferral, but any future panel theme switching logic must remain mindful of this WPF runtime hazard.
2. **Multi-Monitor DPI & Scaling Boundary**:
   - *Code pointer*: [`ControllerWindow.xaml.cs:L39-L42`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ControllerWindow.xaml.cs#L39-L42) & [`LightRingWindow.xaml.cs:L129-L141`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/LightRingWindow.xaml.cs#L129-L141)
   - *Detail*: Overlay and Light Ring windows calculate bounds using `System.Windows.Forms.Screen` and manual DPI scaling transforms. Systems with mixed DPI monitors (e.g. 100% and 175%) rely on Win32 coordinate mapping which requires careful verification if repositioning logic is altered.
3. **Large Single Controller Window (`ControllerWindow.xaml.cs`)**:
   - *Code pointer*: [`ControllerWindow.xaml.cs`](file:///c:/Users/h128/Documents/Projects/stopwatch-main/StopwatchOverlay/ControllerWindow.xaml.cs) (4,546 lines)
   - *Detail*: `ControllerWindow` acts as the central coordinator for timer ticking, overlay window lifecycle, tray icon, settings syncing, hotkeys, and project recovery. While well-structured with `#region`s and helper policies, its size requires careful attention when modifying window event lifecycles.

---

## 13. Review Coverage

### What Was Inspected in Full
- All first-party C# source files in `StopwatchOverlay` (43 files, including all window code-behinds, models, stores, managers, policies, and utilities).
- All XAML markup in `StopwatchOverlay` (`App.xaml`, `ControllerWindow.xaml`, `OverlayWindow.xaml`, `SettingsWindow.xaml`, `ProjectDashboardWindow.xaml`, and all 8 overlay theme dictionaries).
- Project files: `StopwatchOverlay.csproj`, `StopwatchOverlay.Tests.csproj`.
- Documentation and specs: `README.md`, `DEVELOPERS.md`, `specs/ANTIGRAVITY_PROJECT_ONBOARDING.md`, `specs/IMPLEMENT_FLOATING_CLOCK_THEME_SYSTEM.md`, `.codex-stability-repair/repair-log.md`.
- All 15 unit and integration test files in `StopwatchOverlay.Tests`.

### What Was Excluded
- Generated compilation outputs (`bin/`, `obj/`, `release-standard/`, `release-signed/`).
- Raw binary media assets (`.ttf` font binaries, `.jpg` background patterns, `.ico` icons, `.zip` archives).
- Git repository internal database (`.git/`).
- External tools (`tools/ReadmeScreenshots/`).
