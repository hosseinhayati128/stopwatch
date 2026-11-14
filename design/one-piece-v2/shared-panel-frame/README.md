# Shared gold panel frame repair

The original side rails continued behind the transparent openings in the corner and midpoint ornaments. The panel recipe now uses two aligned rail bands and ends each connector at the ornament boundary. Corners and medallions retain their original crops and proportions; the source PNG is unchanged.

`StopwatchFrameSurface` selects this panel-only recipe for `engraved-filigree`. `ThemedClockFace` keeps the original recipe. The renderer cache distinguishes the two, and panel fill uses the same corrected layout as the frame. Other frame designs retain their original layout and cache entries.

## Native evidence

- `before-details.png` and `after-details.png`: actual renderer crops enlarged 5x, showing the top corner, midpoint ornament and bottom corner.
- `before-*` and `after-*`: picker (460x480), Settings (1120x720), and large panel (1400x900), each at 70%, 90%, and 125% artwork scale.
- `settings-native.png`: actual Settings Fonts & colors panel with synthetic settings, rendered on a private Windows desktop.
- `clock-baseline.json`: SHA-256 hashes of raw native clock pixels captured before the repair: all 16 frame designs at 70%, 90%, and 125%. Preserve these baselines; do not regenerate them to accept a change.

## Verification

`PanelFrameChecks` checks ornament openings, connector continuity, all 48 unchanged clock snapshots, exact panel interior masks, content clipping, small-size fitting, bounded cache isolation, native Settings rendering, and picker keyboard behavior. The ornament test detected 186 unwanted pixels before the repair; it detects zero after. The connector silhouette test also rejected an intermediate strip with repeating bumps.

Build the desktop regression harness, then run these modes through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`:

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --panel-frame-checks -LogPath .superpowers/sdd/2026-11-14-panel-frame/final-native.log
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --panel-frame-regressions -LogPath .superpowers/sdd/2026-11-14-panel-frame/regressions-final.log
```

Verified on 2026-11-14: build succeeded with zero warnings/errors; five focused native groups passed; all ten existing frame/overlay/timer regression groups passed, including sixteen timer-shortcut scenarios; 1,052 unit tests passed with none failed or skipped. The broader run exposed stale shortcut subscriptions in the preview test fixture. Its disposal now removes those two subscriptions; no production shortcut behavior changed.

Native checks use the Windows software renderer on a private desktop. Physical-monitor DPI/GPU differences are not separately verified. No new runtime artwork, application settings, installation, restart, or Git publication is part of this repair.
