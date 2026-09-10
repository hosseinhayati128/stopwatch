# Stabilize the Current Stopwatch Overlay App

## Mission

Repair the current local WPF application so it works reliably and fluidly.

For this task, stop all Acanthus design and implementation work. Do not use Figma. Do not redesign the app. Do not restructure unrelated screens.

Focus only on these reported problems:

1. The controls in **Appearance and Background** become stuck or do not respond correctly.
2. In **Pixel Deck Night**, button hover styling should match the floating-clock hover controls and use white text.
3. The controls in **Light Ring** also become stuck or behave incorrectly.
4. The application unexpectedly crashes and closes.
5. General settings interaction should feel fluid, responsive, and stable.

The current local project is the source of truth. Repair it in place while preserving working functionality and user data.

## Important terminology check

The user may be referring to either:

- `Slider` controls, such as opacity, size, thickness, background strength, light-ring brightness, and light-ring width
- `ScrollBar` or `ScrollViewer` controls used to move through settings content

Do not assume which one is broken.

Inspect and test both:

- scrolling through each settings section
- dragging every slider thumb
- clicking slider tracks
- keyboard adjustment of sliders
- mouse-wheel behavior
- interaction after switching themes
- interaction after resizing the window

## Scope lock

Do not:

- continue the Acanthus redesign
- use Figma
- rewrite the UI
- change the application information architecture
- replace WPF
- create duplicate windows
- remove existing themes
- reset user settings
- remove features
- make unrelated visual changes
- perform a broad MVVM migration
- add a third-party UI framework
- push, pull, or commit automatically

A small refactor is allowed only when it directly fixes input handling, responsiveness, crash safety, or duplicated event logic.

## Preserve current functionality

Do not regress:

- all timer modes
- multiple timers
- global shortcuts
- floating overlays
- hover controls
- click-through
- multi-monitor positioning
- combined overlays
- project tracking
- records
- analytics
- custom backgrounds
- background strength
- background opacity
- light ring
- REC indicator
- theme switching
- workspace recovery
- settings persistence
- project-history persistence
- tray behavior
- start with Windows

## Safety snapshot before editing

Before modifying files, protect the current state.

When local version-control metadata is available, create:

```text
.codex-stability-repair/
├── status-before.txt
├── diff-before.patch
├── staged-diff-before.patch
├── untracked-files-before.txt
├── baseline-build.txt
├── baseline-tests.txt
└── repair-log.md
```

Record:

```text
git status --short
git diff --binary
git diff --cached --binary
git ls-files --others --exclude-standard
```

Do not run:

```text
git reset --hard
git clean
git checkout .
git restore .
git pull
git push
```

When no version-control metadata is available, copy every file likely to be changed into:

```text
.codex-stability-repair/backup-before-fix/
```

Do not overwrite unrelated user changes.

## Phase 1: inspect and reproduce

Before changing code:

1. Inspect the current project structure.
2. Inspect all current changes and newly added files.
3. Build the current application.
4. Run the current tests.
5. Launch the app when Windows GUI execution is available.
6. Reproduce each reported issue.
7. Record exact reproduction steps and observed behavior in `repair-log.md`.

At minimum, inspect:

- `StopwatchOverlay/App.xaml`
- `StopwatchOverlay/App.xaml.cs`
- `StopwatchOverlay/AppThemeManager.cs`
- `StopwatchOverlay/AppSettings.cs`
- `StopwatchOverlay/AppBackgroundManager.cs`
- `StopwatchOverlay/ControllerWindow.xaml`
- `StopwatchOverlay/ControllerWindow.xaml.cs`
- any current `SettingsWindow.xaml` and code-behind
- any settings views, user controls, converters, or behavior classes
- `StopwatchOverlay/OverlayWindow.xaml`
- `StopwatchOverlay/OverlayWindow.xaml.cs`
- `StopwatchOverlay/LightRingWindow.xaml`
- `StopwatchOverlay/LightRingWindow.xaml.cs`
- `StopwatchOverlay/Themes/PixelDeck.xaml`
- `StopwatchOverlay/Themes/PixelDeckDay.xaml`
- `StopwatchOverlay/Themes/Midnight.xaml`
- `StopwatchOverlay/Themes/Daylight.xaml`
- any partially added theme dictionaries
- relevant tests

Search the entire solution for:

```text
Slider
ScrollBar
ScrollViewer
Thumb
Track
ValueChanged
PreviewMouse
MouseDown
MouseMove
MouseUp
CaptureMouse
ReleaseMouseCapture
IsHitTestVisible
IsEnabled
Opacity
PanningMode
CanContentScroll
VerticalScrollBarVisibility
HorizontalScrollBarVisibility
BackgroundOpacity
PanelBackgroundStrength
LightRingBrightness
LightRingWidth
ThemeChanged
DispatcherUnhandledException
UnhandledException
UnobservedTaskException
```

## Phase 2: determine the root cause of the stuck controls

Do not apply random template changes before finding the cause.

Investigate these common WPF failure modes:

### Hit testing and overlays

Check for:

- transparent `Border`, `Grid`, `Canvas`, or preview layers sitting above sliders
- invisible controls that still have `IsHitTestVisible="True"`
- popup or drag layers intercepting mouse input
- oversized controls overlapping settings content
- disabled parent containers
- `IsEnabled` or `IsHitTestVisible` inherited unexpectedly

Use temporary diagnostic backgrounds or event tracing only while diagnosing. Remove them before completion.

### Mouse capture

Check for:

- a `Thumb`, slider, drag handler, or overlay window retaining mouse capture
- missing `ReleaseMouseCapture`
- exceptions during drag leaving capture active
- controller window drag logic receiving slider mouse events
- custom window chrome treating slider dragging as window dragging

Ensure mouse capture is always released with `try/finally` where custom capture is used.

### Slider template problems

Inspect global and theme-specific styles for:

- missing `PART_Track`
- missing or zero-sized `Thumb`
- incorrect `Track` orientation
- a transparent track with no hit target
- triggers that set `Opacity=0`
- disabled hit testing in hover states
- wrong `TemplateBinding`
- a theme style unintentionally overriding all `Slider` or `Thumb` controls
- a `Style` without `BasedOn` that discards required default behavior

Prefer native WPF slider behavior with a minimal visual template.

Do not create a custom drag implementation when a normal `Slider` can work.

### ScrollViewer problems

Inspect:

- nested `ScrollViewer` controls
- `CanContentScroll` mismatch
- fixed-height child containers preventing extent calculation
- incorrect `VerticalScrollBarVisibility`
- `PanningMode` interfering with mouse input
- wheel events marked handled by child controls
- custom scrollbar templates with a broken thumb or track
- a parent `ScrollViewer` swallowing slider drag events
- layout transforms that make the visual and hit-test positions disagree

Avoid multiple vertical scroll viewers competing inside one settings pane.

### Re-entrant event handlers

Check whether `ValueChanged`, selection, or theme handlers:

- write a value back to the same control
- repopulate the settings UI while the user is dragging
- trigger theme reapplication on every movement
- recreate the visual tree
- refresh the whole settings window
- close and reopen a popup
- change focus or selected navigation
- cause a binding loop

Use guard flags only where needed and document their purpose.

### Expensive UI-thread work

Check whether slider movement performs expensive operations synchronously:

- saving settings to disk on every pixel of movement
- decoding custom images repeatedly
- rebuilding tiled brushes repeatedly
- recreating every overlay on every value change
- rebuilding charts
- recreating resource dictionaries
- enumerating screens repeatedly
- updating all windows more often than needed

For expensive previews:

- update the numeric label immediately
- debounce or throttle costly rendering
- perform one final apply and save on drag completion
- keep the UI responsive during dragging
- avoid concurrent timers that apply stale values
- cancel obsolete pending updates

Do not move WPF visual-object creation to a background thread. Keep only non-UI computation off the UI thread.

## Phase 3: fix all settings scrolling and sliders

Repair both scrolling and sliders across the full Settings experience.

Test every relevant control, including:

### Appearance

- text color
- outline color
- font
- time format
- text size
- outline thickness
- clock or background opacity
- theme-color or custom-color options, when present

### Background

- background selector
- preview thumbnails
- background strength
- add custom background
- remove custom background
- scrolling through all content

### Light Ring

- enable or disable
- brightness
- width
- hide from capture
- preview response
- scrolling through all content

### Other settings sections

Also verify:

- Overlay and Position
- Behavior
- Application
- theme selection

The fix should be shared. Do not repair only one individual slider while leaving the underlying common style broken.

### Interaction requirements

Each slider must support:

- thumb drag
- track click
- keyboard arrows
- Page Up and Page Down where applicable
- Home and End where applicable
- focus indication
- disabled state
- live label update
- smooth preview update
- persistent final value

Each settings scroll area must support:

- mouse wheel
- scrollbar thumb dragging
- scrollbar track click
- keyboard scrolling
- resizing
- switching settings categories
- returning to the category without losing control state

## Phase 4: Pixel Deck Night button hover

In **Pixel Deck Night only**, normal button hover styling should visually match the floating-clock hover controls.

Inspect:

- `Themes/PixelDeck.xaml`
- the floating overlay action-button style
- `OverlayHoverBrush`
- `OverlayPressedBrush`
- `OverlayChromeBrush`
- `OverlayChromeBorderBrush`
- `OnActionTextBrush`
- the shared normal, primary, danger, icon, and navigation button styles

Implement the following behavior for Pixel Deck Night:

- ordinary button text becomes or remains white on hover
- button icons become or remain white on hover
- the hover background and border treatment match the floating-clock hover-control language
- pressed state remains clearly distinct
- keyboard focus remains visible
- disabled state remains visibly disabled
- hover does not reduce the whole button opacity
- text must not become low contrast
- content presenters and nested icons inherit the intended foreground

Do not change Pixel Deck Day unless it has the same bug and a matching light-theme solution is needed.

Do not change Midnight or Daylight hover styling.

Do not make primary and destructive buttons lose their semantic identity. Their hover treatment may share the same white foreground while retaining suitable primary or danger surfaces.

Validate every Pixel Deck Night button family:

- normal buttons
- primary buttons
- danger buttons
- icon buttons
- navigation items implemented as buttons
- settings actions
- dialog buttons
- pagination
- overlay hover controls

## Phase 5: crash diagnosis and stability

The app unexpectedly closed. Find the actual failure if it can be reproduced.

### Reproduction matrix

Attempt to reproduce while:

- opening and closing Settings repeatedly
- switching settings categories
- dragging each Appearance slider
- dragging Background strength
- adding and removing a custom background
- dragging Light Ring brightness and width
- enabling and disabling Light Ring
- switching themes
- switching to Pixel Deck Night
- opening and closing overlays
- enabling click-through
- opening Analytics and Records
- closing owner windows in different orders
- resizing Settings while interacting
- restarting with saved settings
- running several timers simultaneously

Record the exact last action before any crash.

### Crash logging

Inspect existing application-level exception handling.

If reliable crash logging is absent, add a lightweight local logger under a user-writable application-data folder, such as:

```text
%LOCALAPPDATA%\StopwatchOverlay\Logs\
```

Capture at least:

- timestamp
- app version
- process and thread information
- exception type
- message
- stack trace
- inner exceptions
- current theme
- open window types
- current settings category
- the last non-sensitive UI action when practical

Wire logging for:

- `Application.DispatcherUnhandledException`
- `AppDomain.CurrentDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`

Rules:

- do not swallow unknown fatal exceptions just to keep the process open
- handle only exceptions proven to be recoverable
- prevent recursive logging failures
- rotate or limit log size
- do not log project names, file contents, custom background paths, or other unnecessary personal data
- do not show repeated modal error boxes during a failure loop

### Likely stability areas

Inspect carefully for:

- disposed or closed settings windows still referenced
- duplicate `ThemeChanged` subscriptions
- missing event unsubscription
- multiple timers created each time Settings opens
- callbacks firing after a window closes
- stale `DispatcherTimer` callbacks
- resource dictionary load failures
- missing theme resource keys
- unsafe background image decoding
- exceptions in `ValueChanged`
- `ObjectDisposedException`
- `InvalidOperationException` from window state
- cross-thread WPF access
- `NullReferenceException` during category switching
- collection changes during enumeration
- recursive settings save or apply
- owner window closing before a child
- popup state referencing a closed visual
- light-ring windows being updated after closure
- unbounded preview-image cache
- corrupted settings causing startup failure

Add safe fallback behavior where appropriate:

- if a theme dictionary fails to load, log the error and fall back to Midnight
- if a custom background fails to load, keep the app open and use the theme default
- if settings data is malformed, preserve the current recovery behavior
- if a preview update fails, log it and keep the last valid preview
- if a window is already closed, stop updating it

Do not mask data corruption or silently discard user data.

## Phase 6: fluidity and responsiveness

After functional repair, reduce avoidable UI stalls.

Review:

- disk writes during slider drag
- background decoding
- brush recreation
- settings preview refresh frequency
- overlay refresh frequency
- theme switching
- window open and close
- repeated screen enumeration
- timer tick handlers
- layout invalidation

Prefer:

- debounced persistence
- coalesced preview updates
- cached decoded images
- one active update timer per purpose
- cancelling stale pending work
- reusing brushes and visual resources
- avoiding full-window rebuilds for a single setting change

Do not introduce complex asynchronous code unless measurement shows it is needed.

Do not change timer accuracy to improve perceived UI performance.

## Phase 7: tests

Run the current tests before and after repair.

Add focused tests where practical for:

- setting-value clamping
- debounced or coalesced settings updates
- theme resource-key completeness
- Pixel Deck Night hover-resource values
- loading every theme dictionary
- malformed settings fallback
- custom background failure fallback
- any new crash-log path or log-rotation helper
- any extracted pure settings-update logic

Do not weaken or delete meaningful existing tests.

Run:

```text
dotnet build
dotnet test
dotnet build -c Release
dotnet test -c Release
```

Use the actual solution or project paths present in the local folder.

## Phase 8: manual validation

When Windows GUI execution is available, complete this test matrix.

### Settings interaction

For each theme:

- open Settings
- scroll to the top and bottom of every category
- drag every slider from minimum to maximum and back
- click on each slider track
- adjust each slider with keyboard arrows
- resize the window while a category is open
- switch categories repeatedly
- close and reopen Settings
- restart the application and verify final values

### Pixel Deck Night

Verify:

- ordinary button hover
- primary button hover
- danger button hover
- icon-button hover
- navigation hover
- dialog-button hover
- pagination hover
- overlay toolbar hover

Expected:

- white foreground
- floating-clock-compatible hover surface
- no opacity-only dimming
- readable focused and pressed states

### Stability soak test

Run the app for at least 10 minutes while:

- several timers run
- an overlay is visible
- Settings is opened and closed repeatedly
- Appearance, Background, and Light Ring controls are adjusted
- themes are switched
- Analytics and Records are opened
- one timer is paused and resumed
- click-through is toggled

Confirm:

- no crash
- no frozen control
- no retained mouse capture
- no runaway CPU usage
- no repeated exceptions in the log
- final settings persist after restart

If the environment cannot run the Windows GUI, do not claim these checks passed. List them as required manual validation.

## Completion criteria

The task is complete only when:

- the application builds
- tests pass
- Settings scroll areas work
- all relevant sliders work
- Appearance and Background no longer become stuck
- Light Ring controls no longer become stuck
- dragging sliders remains responsive
- final values persist
- Pixel Deck Night hover uses white foreground
- Pixel Deck Night hover matches the floating-clock hover language
- other themes are not unintentionally changed
- the crash is reproduced and fixed, or robust diagnostic logging is added when reproduction is impossible
- no unknown exception is silently swallowed
- no unrelated user changes are discarded
- no Acanthus redesign work is performed
- temporary diagnostics are removed
- the repair log and backup remain available

## Final report

At completion, report:

1. baseline build and test status
2. exact root causes found
3. files changed
4. settings and slider fixes
5. scrollbar and ScrollViewer fixes
6. Pixel Deck Night hover fixes
7. crash root cause and repair
8. crash logging added or changed
9. performance and fluidity improvements
10. build commands and exact results
11. test commands and exact results
12. GUI checks actually completed
13. manual checks still required
14. unrelated changes preserved
15. remaining known risks

Do not claim that the crash is fixed if it could not be reproduced and no specific failing code path was identified. In that case, state that stability was improved and diagnostic logging was added.
