# Stopwatch redraw cost implementation plan

> **For agentic workers:** Use superpowers:executing-plans for inline execution and one independent final review.

**Goal:** Reduce the measured live Stopwatch CPU/GPU load while preserving timer precision, appearance, opacity, input and data.

**Architecture:** Measure a native hidden overlay receiving the controller's existing 33 ms updates, then prevent redundant text/state/layout/toolbar invalidations. Use the measured result to decide whether static rendering also needs caching; do not change timer integration or display precision to obtain a speedup.

**Tech Stack:** .NET 10, Avalonia 11.2.4, existing private-desktop harness.

**Spec:** User-authorized performance repair following live measurement: 9–11% total CPU, about 12% GPU, 305–320 MB RAM. Current profile is Vine Filigree / Midnight Chart / instrument digital, size 27, whole-second time format. Fix the app's avoidable work; do not claim all machine lag is proven to originate here.

## Global constraints

- All native/unit UI runs on a private desktop. No OS input injection or visible test windows.
- Reuse current checkout; preserve all prior frame, design and unrelated Todo changes.
- Preserve 33 ms timer updates, countdown integration, selected precision, all themes, state transitions and shortcuts.
- Baseline and final measurements use the same native fixture, profile, update count, rendering backend and sample duration. No percentage claim from unrelated runs.
- Prepare and verify the fix before live deployment. No Git publication requested.

## Review focus

- Repeating an unchanged value must keep layout valid; actual text/name/REC changes must still redraw.
- Same-width changing Nika digits must retain stable dimensions; long-hour growth must still remeasure.
- Active/running/clock-mode and selection-opacity changes must refresh the live face and toolbar, including reapplying appearance/theme.
- Offscreen or paused clocks must not keep repainting solely because of repeated controller state updates.
- Native performance evidence must not count shader/font warm-up, readback/export work or a changed timer cadence as the improvement.

## Task 1: Reproduce and remove redundant invalidation

**Files:** `tools/DesktopRegressionChecks/StopwatchPerformanceChecks.cs`, `Program.cs`, `Controls/ThemedClockFace.cs`, `Views/OverlayWindow.axaml.cs`.

- [x] Add a hidden native benchmark for paused and running whole-second/tenth-second overlays using the current profile. Save baseline wall/CPU/allocation/update counts and visual captures before any production changes.
- [x] Add regressions for repeated text/name/REC values retaining valid layout, real changes updating pixels, and stable toolbar brushes/masks on repeated window state. Observe failures against the current code.
- [x] Add narrow equality guards and keep presentation/toolbar work tied to real state or appearance changes. Validate fixed-width digits without suppressing hour-width growth or other-theme measurement.
- [x] Repeat focused checks and the identical benchmark; only pursue static-layer rendering changes if the remaining cost is material.

## Task 2: Verification and ready build

- [x] Run hidden full unit suite, shared-frame regression, native state/opacity/theme restoration, picker/timer shortcuts and new performance regressions. Preserve prior review images by redirecting output.
- [x] Compare native baseline/final measurements, record practical limits, inspect changed files and get one independent review of this repair.
- [x] Fix important review findings with reproducing tests; build the release candidate and record hashes. Handle live installation only after checks pass and the running state can be preserved safely.

## Completion

Completed and installed after the user approved the restart. See `docs/stopwatch-redraw-performance-2026-11-05.md` for measurements, limitations, independent review, regression results and rollback hashes. The presentation and mask fixes retain vector rendering; no static raster-layer cache was needed.
