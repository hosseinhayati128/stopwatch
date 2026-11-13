# Settings & Overlay Inspector — first layer

Native production captures from `SettingsWindow`, using synthetic settings on a private Windows desktop. The installed app is not changed by this implementation task.

Nika now uses the approved standalone shared frame, grouped navigation for all thirteen categories, a fixed Done/status footer, and the responsive Overlay & Position page. The wide page puts placement and interaction controls beside the actual clock renderer; the compact page puts a short preview above side-by-side selectors. Other categories retain their existing editors and specialized previews. Switching application themes restores the legacy shell and its theme-dependent controls.

Display, Position, Click-through and Hide from screen capture retain immediate application/persistence. Save failures remain visible in the footer. Show / hide active overlay now calls the existing controller action without changing timer data. Display/preset changes reach the desktop overlays; All displays mirrors the same logical clock, while a specific display removes other copies. Placement uses monitor working areas and scaling; separated clocks use stepped positions, and manual positions use the existing per-timer/per-display workspace fields. Stable timer ticks retain the same windows and rendering resources.

## Captures

- `overview.png`: 1120 × 720 DIP, actual clock renderer and real enumerated display labels.
- `compact.png`, `compact-controls.png`: 840 × 600 DIP, initial placement controls and scrolled interaction section.
- `interaction.png`, `custom-position.png`, `save-failure.png`: production states and persistence feedback.
- `StopwatchAppearance-retained.png`, `TodoAppearance-retained.png`: existing editors retained inside the new shell.
- `legacy-midnight.png`: restoration to the existing dark-theme Settings page.

## Verification commands

Final result: nine native groups and all 1,052 unit tests passed. The desktop/harness build has zero warnings/errors. The unit build had two NU1900 warnings because the package-audit service was unavailable. A fresh read-only review found one dark-theme restoration defect; the regression reproduced it before the fix and passed afterward. The installed executable and six unrelated Todo preview images were verified unchanged by hash.

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj` with `--no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true`.

Run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` with `--settings-shell-native`, `--settings-shell-regressions`, and `--unit-tests`. Every runner uses a private desktop without switching the user's desktop. Build the unit project before running `--unit-tests`.

The native fixtures exercise navigation, initial-load isolation, compact layout and resizing, immediate changes, save feedback, theme restoration, keyboard focus/traversal, active visibility, synthetic display geometry, mirror cleanup and timer-state preservation. Existing inspector/controller/resource-cache checks cover adjacent behavior. Physical multi-monitor hot-plug, Linux native drag completion and capture exclusion/click-through still require live hardware validation. Synthetic geometry and private-window checks are not substitutes for those checks.

No new runtime artwork, dependencies or continuously running preview timers were added.
