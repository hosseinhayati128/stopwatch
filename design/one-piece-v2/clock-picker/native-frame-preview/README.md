# Existing frame at three sizes

User-requested native-renderer design spike, 2026-11-05. This is a visual comparison, not an implemented picker or settings redesign.

## Reproduce

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then run from the repository root:

```powershell
powershell -NoProfile -NonInteractive -WindowStyle Hidden -File tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--frame-size-preview' -LogPath '.superpowers/sdd/2026-10-31-stopwatch-overlay/frame-size-preview.log'
```

The launcher creates a private Windows desktop and never switches to it. This route initializes only Avalonia drawing services: no application lifetime, native windows, production settings, controller, or hotkey registration.

## Images

- `same-frame-three-sizes.png`: all three exports placed on one board at the same logical scale.
- `stopwatch-actual-renderer.png`: existing `ThemedClockFace`, default 48-point time, 412.8 x 132 frame plus the 12-unit active-tab allowance.
- `clock-picker-frame-preview.png`: 460 x 480 frame with static compact-list sample content.
- `large-panel-frame-preview.png`: 820 x 650 frame with static appearance-panel sample content.

All use the same existing `engraved-filigree.png` (551,375 bytes), unchanged. All frame drawings call `StopwatchAppearanceAssets.DrawFrame`; both larger examples also invoke the existing `StopwatchFrameMask.Create` and `DrawCover`. Corner scale remains 0.18, giving source-slice corner regions of 52.2 x 28.8 logical units. The background is the existing Navigator Parchment at 70%, shown over navy.

No new runtime image resources were created. These PNGs are review exports under `design/`, not bundled frame assets. Production source files, application behavior and saved user settings are unchanged.

## Findings

- The frame is reusable at all three dimensions without generating another source PNG.
- Corner ornament proportions remain fixed.
- The existing nine-slice implementation stretches decorative side sections substantially on tall panels. Reusing it unchanged is not the finished picker design.
- Next proposed refinement: separate fixed side ornaments from plain extendable rails, or repeat suitable edge segments. Preserve this captured baseline when comparing that refinement.
- The reused background currently uses cover/crop. A tall panel reveals a different crop and larger map details; background material/motif scaling remains a separate design decision.

## Verification and limits

Build completed with zero errors and warnings. The private-desktop preview completed successfully; source SHA-256 before and after matched. `verification.json` records the exact hash, dimensions and diagnostics.

Frame-only diagnostic captures at 96, 144 and 192 DPI compare all four corners between pixel-aligned 420 x 132, 460 x 480 and 820 x 650 rectangles. The 24 comparisons show small nonzero raster differences in some corners: maximum per-channel difference 11/255; maximum mean channel difference under 0.03/255. These are diagnostic measurements, not a claim of byte-identical corners. The first strict maximum-delta assertion stopped at 8/255; it was replaced by complete reporting rather than silently broadening a pass threshold. No renderer fix is claimed.

Final review exports use 96 DPI. Initial higher-DPI full-clock `RenderTargetBitmap` exports displaced opacity-masked text; using the established 96-DPI path and composing its snapshots produced correct text. That higher-DPI export issue remains uninvestigated and is not evidence about physical monitor behavior. Only frame drawing was inspected at all three export resolutions. System DPI and the active desktop were not changed.

Only Engraved Filigree was assessed. This does not establish suitability of the other 15 frames, interactive behavior, accessibility, adaptive layout or production readiness of the sample panels.
