# Nika's Chronicle — implementation validation

Implemented on 2026-10-26 in both the Windows WPF and Avalonia frontends. Select **Settings → Appearance → Application theme → Nika's Chronicle**. The original **one piece** theme remains a distinct choice.

The new theme exposes two map treatments, four typography styles, and the original warrior plus sixteen sidebar alternatives. The three preferences normalize and persist independently of global typography and floating-clock choices. Per the user's correction, every non-controller page uses the original One Piece resources and layouts. The floating clock uses One Piece when following Nika; explicitly selected clock themes are preserved. The navy/parchment resources are local to the controller and are removed when switching away.

## Reference-fidelity correction

The initial implementation was rejected for changing non-controller pages and drifting from the approved panel. The correction uses the approved 1536×1024 image as the geometry reference: inset rounded frame, 96px header, 360px rail, icons and real overflow menus, heavy serif headings, condensed Black timer digits, one segmented mode capsule, arc marks, inline Edit, real three-column lap table, and a55px footer. Settings is rendered separately to verify inherited One Piece wood, brass and parchment. Generated illustrations and font rasterization can still differ from the source concept; these native controls are not a baked screenshot.

## Evidence

### Artwork proportions and compositing correction (2026-10-27)

The user supplied `ChatGPT Image Oct 26, 2026, 08_58_23 PM.png` as the current reference and identified distorted cartography, a different Log Pose, missing header illustration, elliptical timer arcs, a rectangular mural crop, and crude brass rules.

The Desktop artwork renderer now crops map/navy sources uniformly to cover their viewports, retaining round compass features. Timer arcs are independent circular sections with a shared radius rather than one stretched ellipse. The header uses uniformly scaled, feathered cartography/island regions from the latest supplied reference, with no reference labels. The crest uses its own isolated reference region. The Log Pose uses a new transparent extraction with more restrained glass/brass and restored visual size/offset. A flat transparent bronze mural replaces the opaque screenshot crop. Reference-relative timer width and tighter rail line spacing keep art clear of the controls. Secondary buttons use a dark outer keyline, fine graduated brass rule, and subtle navy plate.

Fresh focused checks cover uniform source/destination scale, actual rendered circle samples at two widths, transparent mural corners, native/resized/maximized bounds, visible lap rows, typography, theme restoration, click targets, and all mode selections. Sample-data renders remain application renders rather than screenshots of a user's live timer workspace. The standard Windows package is staged in `dist/desktop-windows-ready`. The saved settings and running timer data are not changed.

Built-in imagegen extraction/alpha-repair prompts and final artwork paths are recorded in `artwork-refinement-prompts.md`.

### Typography correction (2026-10-26)

The previous controller used Times New Roman headings and constrained the timer too tightly. Original Classic now embeds static Playfair Display ExtraBold (800), retains the verified Roboto Condensed Black (900) timer, increases the timer's prominence, uses separate Barlow Condensed lap numerals, and aligns the unit labels with actual columns. Action/rail labels are larger and clearer. Font metadata and Avalonia's resolved glyph faces were inspected, rather than relying on requested weights alone.

The user's saved profile was `navigator-precision`. That retained sample now uses embedded Inter ExtraBold display headings and Rajdhani SemiBold technical numerals, while retaining serif section labels, matching its generated reference more closely. The saved profile was not overwritten. All new fonts are static files from the official Google Fonts distribution with their SIL OFL texts alongside them.

Fresh Release build and focused controller checks pass. A new regression rejected the old Times New Roman heading, verifies actual face names/weights for Classic and Navigator, and checks timer prominence at normal/maximized sizes. The layout checks now inspect the rendered lap-row bottoms against the footer, rather than only the containing ItemsControl. Mode-click persistence, theme restoration, native button hit-testing, and flush frame geometry also pass. Current sample-data renders: `implemented-desktop/controller-maximized.png` (Original Classic) and `controller-navigator-precision.png` (the user's selected profile).

### Mode selection correction (2026-10-26)

Clicking Clock could revert to Stopwatch on the next display refresh. `ModeRadio_Checked` read the whole radio group while the previously checked button could still be set, and it also handled programmatic updates despite `_updatingUi`. The handler now ignores display synchronization, derives the mode from the event's selected button, and updates the displayed state immediately. Entering Clock remembers the preceding non-clock mode.

The new `ControllerModeChecks` regression reproduced the failure before the fix (`clicking Clock reverted to mode 0 during refresh`). It now passes actual Avalonia radio-button `OnClick` transitions through all four modes, three refreshes per selection, countdown-panel visibility, and loading stored modes without rewriting timer state, across Nika, One Piece, and Midnight. The complete focused Nika layout/theme/input suite also passes. Release build: zero warnings/errors. The corrected Windows single-file package is prepared in `dist/desktop-windows-ready`; the normal `publish.ps1` workflow rebuilds this same Desktop project.

### Border and spacing refinement (2026-10-26)

The first native-size correction still fitted a fixed 1496x984 panel, plus a 20-unit margin, into differently proportioned windows. This produced the outer empty bands reported by the user. A new frame-boundary regression reproduced the defect at 1040x720: the frame started at (13.54, 26.875) rather than the window edge.

Nika now computes a uniformly scaled canvas with the **same aspect ratio as the native client area**. Its frame has no outer margin, its header/body stretch with that canvas, and a subdued one-pixel edge replaces the inset cyan outline. The approved artwork and control proportions remain; fonts and icons are not stretched. This supersedes the fixed-canvas approach described below.

The visual cleanup also replaces inconsistent Unicode action glyphs with vector icons, makes the mode selector one rounded capsule, removes the selected rail card's double border, lightens action-button outlines, keeps the dial ticks clear of adjacent controls, and improves header/footer legibility. The changes are scoped to Nika's Desktop controller.

Fresh Release build and the focused Desktop checks pass at 1040x720, 1040x520, 600x720, 600x520, the display-clamped wide window, and native maximized 1280x720. Checks assert that all four frame edges meet the client edges, primary action targets remain clickable, laps remain visible, and other themes restore ordinary layout. `implemented-desktop/controller-maximized.png` and `controller-native-launch.png` are current isolated production-control renders with sample data.

### Native Desktop launch correction (2026-10-26)

`publish.ps1` builds and launches **Avalonia Desktop**, not WPF. The earlier WPF image was incorrectly presented as evidence for that launch path. The earlier Desktop capture also manually arranged a 1536x1024 tree, bypassing real native window constraints; it did not establish normal-launch fidelity.

Reproduction at the actual 1040x720 launch size exposed hidden header navigation, cramped sidebar labels, and clipped laps. A regression asserting visible navigation failed before the correction. Nika now preserves its reference composition in a proportional Viewbox; the compact/short layout rules no longer dismantle it. Other themes use an unscaled canvas constrained to their native client area.

Fresh Release build and `DesktopRegressionChecks --nika-only` pass. Checks cover native startup, widths requested at 1400/1040/600 (Windows clamps 1400 to the available display width), full action bounds, actual input hit-testing, visible lap rows, and restoring Midnight and original One Piece dimensions. Captures use the already-arranged frame and its native transformed bounds, without manually arranging or changing root geometry. Avalonia 11.2's ImmediateRenderer does not transform its culling rectangle through a Viewbox; the capture draws that frame through a VisualBrush at those native bounds to avoid capture-only omissions. These are isolated application renders with synthetic data, not screenshots of the user's running instance.

The corrected single-file Windows build is staged separately in `dist/desktop-windows-ready` using the same project/configuration/runtime/bundling options as `publish.ps1`. The older running `dist/desktop-windows` instance is preserved; running `publish.ps1` will rebuild and replace it through the user's usual launch workflow.

`implemented-desktop/controller-native-launch.png` is the corrected normal-launch render. The older verification results below are retained as history, not proof of the original published application's layout.

Latest correction validation: Windows Release regression suite **1,000 passed, 0 failed**; final WPF production-XAML preview harness passed all render, resource, switching, compact-scale and safe lap-format checks. Avalonia Release build **0 warnings/errors** and `DesktopRegressionChecks --nika-only` passed on the final controller tree, including narrow-width actions, menu contrast, original One Piece global resources, local palette removal and all retained selectors. The WPF mural crop was subsequently bounded explicitly to prevent ImageBrush viewbox bleed; its build/render was repeated.

- `dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj -c Release --no-restore -m:1 -nr:false --verbosity minimal`: **999 passed, 0 failed**. One NU1900 warning: NuGet vulnerability service unavailable. The test host required execution outside the restricted process sandbox for IPC.
- WPF Release and `tools/PiratePreview --nika-only` production-XAML renders passed. Captures cover paused/running, synthetic hover/focus, compact 125% scrolling, four typography choices, Amber Atlas, Settings and switches back to Midnight/original one piece. All seventeen mural resources and crops resolved.
- `dotnet build tools/DesktopRegressionChecks -c Release --no-restore -m:1 -nr:false`: **0 errors, 0 warnings**.
- `dotnet run --project tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj -c Release --no-build --no-restore -- --nika-only`: **passed**. Checks exercise selectors and field isolation, controller widths 1400/1040/600, primary-action palette, secondary-action interaction text, actual submenu template contrast, and restoration of ordinary timer controls.
- Full DesktopRegressionChecks: **15 top-level passes, 1 failure**, plus a nested bidi pass. The failure is the task-list expansion check, reporting missing/clipped existing task labels. An isolated `--todo-add-only` run also fails before selecting Nika; diagnostic inspection found zero task rows. Task-list production source was not changed. A clean baseline checkout was not tested, so this is not claimed as a proven preexisting failure.
- Independent code review caught selected-tab contrast and a hidden menu; both were corrected. Final `git diff --check` passed.

## Preview files

- `implemented-wpf/controller-paused.png`: production WPF controller XAML with synthetic timer data.
- `implemented-wpf/settings-appearance.png`: WPF appearance selector layout.
- `implemented-desktop/controller-paused.png`: actual Avalonia controller render with synthetic timer data.

These are isolated renders, not evidence of live OS keyboard/mouse operation. The user's running app and saved timer/project data were not replaced. No deployment, commit or push was performed.

Artwork generation prompts and embedded locations are documented in `production-assets.md`.

## 2026-10-28: Ten additional backgrounds and edge detail

- User approved all ten generated maps, then requested sparse lighter detail along the left and bottom. Refined each map using built-in imagegen; preserved the original files, revision prompts, and source paths in `background-options-2026-10-27/`.
- Added all ten numbered options after Original Map and Amber Atlas in the shared catalog. Both app versions embed byte-identical copies of the refined PNGs and use the shared normalized asset mapping. Current defaults and independent typography/sidebar choices remain unchanged.
- Avalonia uses its existing uniform cover crop. WPF uses a uniformly scaled, right-aligned brush for new maps. Original background treatments remain intact.
- Extended existing persistence tests: 14 passed. Before implementation, the ten unregistered IDs and expected catalog count failed as intended. NuGet vulnerability metadata was unavailable during WPF builds; compilation and settings tests subsequently completed successfully.
- Fresh Desktop Release build: zero warnings/errors. Nika checks passed: 12 distinct embedded backgrounds rendered, every selector committed independently, frame/layout/typography checks passed, controller mode selections survived refresh, and hotkey checks passed.
- Captured every new map using actual Avalonia controller controls with synthetic preview timers; visually inspected Grand Line Vellum, Winter Island Log, and Lagoon Cartography. These captures are not screenshots of the user's running application.
- Focused independent review found no important issues in catalog, asset embedding, settings refresh, or cross-app mapping. Preexisting typography and layout differences were outside this change.
- Disk exhaustion required removing only validated Git-ignored bin/Release and obj/Release outputs after the corresponding tests finished. Exact paths and SHA-256 manifests were saved beside the collection. Source, artwork, persisted settings, and the running dist application were preserved.

- Staged Windows single-file publish completed successfully: dist/desktop-windows-ready/StopwatchOverlay.Desktop.exe, 109468942 bytes, 2026-10-28. The user can run publish.ps1 to rebuild/relaunch the normal dist application.

## 2026-10-28: Default selection and publication checks

- Navigator's Masterwork is now the default for new, missing, or invalid Nika background settings. Existing valid saved selections remain unchanged. Runtime checks covered JSON defaults, normalization, asset mapping, and all twelve retained selections; the Windows single-file publish succeeded after this change.
- Fresh publication test run: 1,009 isolated tests passed. The initial unfiltered run additionally failed `MigrateLiveUserData_IfFileExists`; inspection showed that this test attempts to migrate real AppData history and the configured Obsidian log. It was excluded from the subsequent run. The live history file's modification time remained unchanged.
- Fresh Nika desktop checks passed: all twelve embedded backgrounds rendered distinctly, typography and native layouts resolved, theme restoration and controller mode persistence passed, and five hotkey checks passed. These checks use synthetic data.
- Disk exhaustion interrupted the regression harness's dependency-copy step. The compiled harness was run successfully with `dotnet exec` from the Desktop output directory, using its runtime configuration and dependency manifest. This avoided another full dependency copy. Disposable build outputs were cleared afterward; source assets and the staged Windows package were retained.
- Original generation prompts now identify the refined files installed in the apps. Local cleanup audit files are ignored by Git.
