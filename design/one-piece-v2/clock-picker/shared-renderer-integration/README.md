# Shared renderer integration

Completed 2026-11-05. The production stopwatch overlay, appearance preview, Nika clock picker and reusable panel surface now share the reviewed modular frame recipes. The 16 existing frame PNGs are unchanged; no runtime artwork or dependencies were added.

Corners and ornaments retain uniform proportions. Rails repeat with a clipped final segment. Very small destinations reduce the whole recipe uniformly. Background clips come from the assembled border, including separate Tide Curve interior spans; open corners use an explicit rounded interior. Content insets keep text and panel children inside the frame. Heavy ornaments may increase overlay height while preserving the selected font size. The 70% background, 40% inactive treatment, ACTIVE tab and controls remain intact.

The Nika picker uses the default Engraved Filigree frame and its existing dialog material. It retains the current list styling and selection behavior. Initial keyboard focus now reaches the selected row. Other themes retain their existing appearance. This completes shared frame infrastructure; picker row styling and typography remain future design layers.

## Evidence

- `shared-renderer.png`: actual stopwatch control, native picker content and an 820 × 650 shared panel.
- `all-frame-overlays.png`: all 16 frames on actual stopwatch controls.
- `frame-catalog/verification.json`: 48 native size samples, 24 additional 1.5×/2× exports, source hashes and 6,223 uniform-scale recipe placements.
- Final native regression: 16 groups passed, including 16 timer shortcut subcases, picker digit/numpad/Enter/Escape selection, initial focus, state-stable geometry, opacity, theme restoration, preview isolation, mask containment and bounded cache reuse.
- Unit suite: 1,052 passed; zero failures or skips. Unit and native suites ran on private Windows desktops without switching the active desktop or generating OS keyboard input.
- Final desktop/harness build: zero errors and warnings. The unit-project build had two NU1900 warnings because vulnerability metadata was unavailable; dependency versions were unchanged.
- Independent review's Tide Curve mask finding was reproduced before correction and passed afterward. The existing opacity test caught the translated geometry group's fill-rule issue; it also passed after correction.

The cache retains at most 32 layouts, including piece placements, masks and insets. Avalonia's drawing-group recorder does not implement the needed cropped bitmap operation, so cached placements replay per paint. No frame-rate improvement is claimed.

Explicit native rendering at 1×, 1.5× and 2× passed, including nonzero-origin wave clips. Physical monitor DPI was not exercised. Export PNG metadata remains 96 DPI; higher-resolution filenames describe equivalent pixel dimensions.

## Reproduce off-screen

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj` and `StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj`. Run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` with each mode below and a repository-local `-LogPath`:

- `--shared-frames-regression`
- `--frame-production-audit`
- `--unit-tests`

Run these modes sequentially. Logs and the unit TRX for this implementation are under `.superpowers/sdd/2026-11-05-shared-modular-frames/`. No commit, push, live installation or user settings changes were performed.
