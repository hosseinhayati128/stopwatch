# Stopwatch redraw repair — 2026-11-05

The repair is installed at `dist/desktop-windows/StopwatchOverlay.Desktop.exe` and the app was restarted with the user's approval. At installation time the old process had already exited. The original executable was copied and hash-verified before replacement. The restarted process loaded the existing 12-timer workspace and continued saving it; two native app windows were visible and the process remained responsive.

## Changes

- Repeated text, normalized names, REC values and presentation states no longer request redundant painting or layout.
- Changing fixed-advance Nika digits repaints without another layout pass. Extra hour digits/precision still request measurement; other themes retain their measurement behavior.
- Repeated controller ticks reuse the applied face/toolbar presentation and opacity masks. Appearance, theme and direct settings updates invalidate that cache.
- The background mask's overlapping scanline rectangles are simplified once on layout cache misses. Text and names use the frame's fitted content rectangle as their clip. This retains vector clipping without introducing raster-layer resolution or lifetime state.
- Timer cadence, configured precision, countdown integration, assets and command behavior are preserved.

## Evidence

The matched private-desktop benchmark used the current Vine Filigree / Midnight Chart / instrument-digital profile at size 27, with 180 controller-style updates at the existing 33 ms requested interval. Warm-up, image export and readback were outside the timed intervals. Each run completed the same update count and distinct displayed times.

| Running display | Baseline CPU seconds | Final CPU seconds | Reduction |
| --- | ---: | ---: | ---: |
| Whole seconds, 6 distinct times | 2.312500 | 0.968750 | 58.1% |
| Tenths, 60 distinct times | 2.453125 | 1.265625 | 48.4% |

The short-sample comparison gate requires at least 40% less CPU work for both running cases. A strict 50% boundary was crossed by ordinary run variation (the tenth-second case measured 50.3% and 48.4% less in consecutive final builds); the final gate keeps margin for scheduling/process-time granularity. Both earlier guards-only and masks-only versions failed the material-running-improvement criterion. This is a benchmark acceptance decision, not a claim of an exact percentage on every machine.

The first paused scenario includes unisolated first-scenario work despite warm-up. Its large raw reduction is not reported as steady-state idle savings. This limitation was identified in independent review. Static bitmap caching was avoided: simplifying the vector mask adds work only when building a layout and avoids extra per-overlay raster memory and DPI behavior.

After installation, live samples measured 1.45% and 0.98% total CPU, and 1.75% GPU usage on the app's 3D engine. The original live samples were 9–11% CPU and about 12% GPU. These live samples are observations across a restart, not a controlled identical-workload GPU benchmark or proof that every cause of system lag is resolved. Memory reduction is not claimed: the fresh process had about 672 MB working set / 486 MB private bytes, versus the older process's approximately 320 MB / 506 MB after running longer.

Verification: 20 native regression groups and 16 timer shortcut subcases passed on a private desktop. This includes all 16 frame masks, Tide Curve recesses at explicit scales, typography/background coverage, picker behavior, state/opacity changes, theme restoration, preview isolation, long hours and fixed-width digit painting. Baseline/final captures had equal dimensions and maximum channel difference 1/255. Debug and Release/publish builds succeeded without warnings/errors.

The full unit suite passed 1,052/1,052 with zero skips on retry. The initial run had one failure in unchanged `CrashLogger_RedactsPrivatePathsRotatesAndNeverMasksFailure` because `TryWrite` returned false; concurrency with other diagnostic writers is plausible but not confirmed. Both logs are retained. No Core or unit-test source was changed for that failure.

Independent review found a direct-settings cache invalidation issue. Its native regression reproduced the wrong inactive-toolbar alpha before the fix and passed after the fix. No additional production findings remain. Other design, shared-frame and Todo changes in the checkout were preserved. Verification and live installation preceded Git publication.

## Build and rollback evidence

- Installed repair SHA-256: `B3DFE6C04C27CB2D4257115245B35872A9F67059973D9417B707B26B5F0A092D`.
- Original executable SHA-256: `68DA5A91AAD241C8A7BFD9872C271331685740F3D7A418BD6B41B519963FB9BB`.
- Original backup: `.superpowers/sdd/2026-11-05-stopwatch-redraw-cost/installed-backup/StopwatchOverlay.Desktop.exe`.
- Native measurements, captures, source baseline, logs and unit TRX: `.superpowers/sdd/2026-11-05-stopwatch-redraw-cost/`.
- Reproduce regression: `Run-HiddenDesktop.ps1 -Mode '--stopwatch-redraw-regression'` with a repository-local log.
- Reproduce benchmark: `--stopwatch-performance-baseline` / `--stopwatch-performance-final` on the corresponding builds, then `python tools/DesktopRegressionChecks/compare_stopwatch_performance.py`. Preserve matched build configuration, backend and workload.

The small numeric benchmark records are retained in `docs/performance/stopwatch-redraw-2026-11-05/`. Compare them with `python tools/DesktopRegressionChecks/compare_stopwatch_performance.py docs/performance/stopwatch-redraw-2026-11-05`. Local logs, executable backups and build outputs remain excluded from Git.
