# Stopwatch overlay implementation verification

Updated 2026-11-04. Working branch: `codex/stopwatch-overlay`, base `7b8efd2558d75e9fcc49a6979f31a38de238f8fd`. Changes are uncommitted; no installed application or real settings were replaced.

## Delivered

The Avalonia stopwatch has independent selectors for all 12 backgrounds, 16 frames, 10 typography profiles, 3 artwork treatments and 12 hover controls. The approved default is Navigator Parchment / Engraved Filigree / Navigator Bold / Clean / Filigree Keys, with 70% background and 40% unselected opacity. Running/paused project markers and the fixed ACTIVE tab replace the old selection underline for this theme.

Settings → Stopwatch appearance contains Style and Transparency tabs, shared text size, immediate apply, save-error feedback and local running/paused/light/dark preview controls. Use Nika overlay changes only the overlay theme. Legacy fields and explicit text colors remain intact. WPF retains its prior Pirate rendering for Nika at the WPF palette boundary.

## Commands and evidence

Logs are under `.superpowers/sdd/2026-10-31-stopwatch-overlay/`.

| Check | Result | Log |
|---|---|---|
| `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1` | Passed, 0 warnings/errors; builds Desktop dependency | `review-fix-build.log` |
| `dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj --no-restore -m:1` | 1052 passed, 0 failed, 0 skipped after review fixes | `review-core-green.log` |
| Native `--stopwatch-appearance-only` | All 5 stopwatch groups passed, plus 5 shortcut checks after review fixes | `review-native-green.log` |
| Native `--nika-only` | Passed controller resources, theme restoration and four-mode refresh | `nika-final.log` |
| Native `--todo-appearance-only` | Passed settings, native overlay, inspector/dialog and isolated fixture checks, repeated after review fixes | `review-todo-green.log` |
| Full native regression suite | Passed all existing desktop checks | `desktop-full.log` |

Native commands use `powershell -NoProfile -NonInteractive -WindowStyle Hidden -File tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode <mode> -LogPath <log>`. `--all` runs the harness with no filter. This creates a private Win32 desktop and never switches the user's desktop. Fixtures close their windows. Core tests use no visible UI; the existing WPF fixture has zero opacity, off-screen coordinates and no activation. Installed dependencies were used because NuGet vulnerability metadata was unavailable (NU1900 in the WPF test dependency); no package changes were made.

## What was checked

- Complete retained-ID coverage, 40 raster definitions, font family/glyph loading and license paths. All 192 background/frame combinations were exported and visually inspected; all typography, artwork and toolbar combinations were reviewed.
- Captains Serif now uses lining/tabular figures. Glyph advance is stable for 00/11/88, hours beyond 99, sizes 16/48/120, empty/long/mixed Persian names and REC. Full names remain accessible.
- Controlled pixel alpha: background 70%, unselected clock/control 40%, unchecked unselected control 28%, all unchecked at 0% transparent. Compositing uses an isolated opacity mask to avoid overlapping draws making a dimmed clock too opaque.
- All four timer modes and selected/unselected × running/paused combinations in both herd and separated clocks. Appearance edits preserve selected session, elapsed time, mode and running state. Clock mode disables pause. Hide/show reaches both overlay types.
- Native TOPMOST, NOACTIVATE, click-through and capture-exclusion API flags; right-click/action dispatch; 200ms popup departure and clock-to-toolbar crossing; Nika/legacy theme round trips without duplicate actions.
- Inspector at 840×600 and 1060×740, native 150% scaling: no horizontal overflow, preview and Done visible, all dropdowns open and reach their last choice, keyboard focus and arrow navigation work. Independent selector counts/defaults, single change events, save-failure feedback, Artwork checkbox memory and reopen persistence pass.
- Preview toggles serialize identically before/after and issue no settings changes. Fixture checks compare actual settings-file bytes before/after. Controller fixtures use explicit synthetic settings and avoid persistence callbacks.
- 120 repeated UpdateTime/render/readback iterations retain the same cached bitmap definitions. Final observed current-thread allocations: 40,925,128 bytes including the fixture's bitmap/readback allocations; this is a resource-reuse probe, not a production performance benchmark.
- Preview clipping regression: button glyphs existed but disappeared when composed under the Viewbox/opacity mask. Removing their redundant rectangular clip restores the three actions; a rendered-pixel regression distinguishes the preview with/without glyphs. Legacy clip values are restored on theme exit.

## Visual evidence and provenance

- `live-default-window.png`, `default-size-16.png`, `default-size-48.png`, `default-size-120.png`.
- `matrix-*.png`: all 12 backgrounds × 16 frames.
- `typography-all.png`, `artwork-all.png`, `controls-all.png`.
- `settings-stopwatch-840-tab-0.png`, `settings-stopwatch-840-tab-1.png`, and matching `1060` files.
- Asset definitions and source rectangles: `StopwatchOverlay.Desktop/Assets/Stopwatch/asset-manifest.json`.
- Generation prompts/source records: `asset-prompts.md`, `generated-assets.json` beside this report. The original Luffy atlas cell is treated at render time; its source image is unchanged.
- Font sources, hashes and licenses: `StopwatchOverlay.Desktop/Assets/Fonts/Stopwatch/README.md` and OFL files. Existing bundled families are referenced in the asset manifest.

## Limits

Host: Windows NT 10.0.26200, Avalonia 11.2.4, software rendering, native 150% scale. Physical 100%/200% display configurations, mixed-monitor dragging, hardware pointer dragging, end-to-end close/reset confirmation dialogs and external capture applications were not exercised. Capture exclusion was verified through the Windows API, not an external recording application. Linux/macOS checks were not run. Active-desktop input and system display settings were deliberately left alone to respect the user's test-isolation requirement.

## Independent review and fixes

A fresh-context gpt-6-astra reviewer inspected the tracked diff, untracked implementation, specification, plan, rulings, tests and sampled contact sheets without editing or launching UI. No critical defects were found. Two actionable findings were repaired in one focused pass:

1. **Malformed new JSON field could discard otherwise valid settings.** Added a profile-scoped defensive JSON converter so only the affected field receives its default, and limited legacy migration to missing/null profiles. Added 13 cases through the real SettingsStore.Load path for wrong field types, nulls, overflowing numbers and invalid profile shapes, retaining valid sibling/unrelated preferences. The original code failed 12 new cases; final full suite passes 1052/1052 (`review-settings-red.log` → `review-core-green.log`). Wholly invalid JSON retains the existing backup recovery behavior.
2. **Shared text-size controls could disagree.** Reviewer labeled this Minor; regraded to Important because adjusting a stale slider could overwrite the user's actual size unexpectedly. Synchronize both controls and labels under initialization guards and on category entry. Native regression observed stale80/48 failure and now passes synchronization with one change event (`review-inspector-red.log` → `review-native-green.log`).

No review findings remain deferred. No second reviewer was dispatched; failed-then-passing regressions verify the fixes. The reviewer declined unavailable physical/platform/input checks, exhaustive independent inspection of all192 images, and performance extrapolation; these remain the explicit limits above, supported by implementer visual inspection and resource-identity checks.

## Execution decisions

The ledger contains the detailed rulings. Existing checkout and dirty work were retained; task bookkeeping used PowerShell/Python because Git Bash sandbox startup failed; local testhost IPC needed unsandboxed execution. Native harness uses the Desktop runtime/deps files. Font mappings use licensed bundled equivalents after live inspection. Isolated masks provide the specified numeric alpha. Inspector reopen checks first instantiate the selected category. Legacy WPF maps Nika to Pirate at its own boundary. Native150% is verified; other physical scales and active-desktop interactions remain explicit limits. These choices avoid unrelated changes; the main residual risk is behavior on untested display/platform configurations.
