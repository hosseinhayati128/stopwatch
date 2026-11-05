# Shared Modular Frames Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Integrate the reviewed, non-stretching frame recipes into the native application and reuse them across overlay and panel sizes.

**Architecture:** Production recipes produce fixed-scale placements. A bounded shared cache stores immutable artwork placements, matching interior clips and safe content insets by frame/size/scale. Overlay and panel controls consume the same layout. Backgrounds and appearance settings stay independent.

**Tech Stack:** C# / .NET 10, Avalonia 11.2.4, existing PNG resources.

**Spec:** `design/one-piece-v2/clock-picker/design-exploration.json` and `native-frame-join-refinement/visual-review.md` in that directory. User approved integration on 2026-11-05; native execution and hidden testing are already authorized.

## Global Constraints

- No artwork stretching, including edge strips and ornaments. Rotation/reflection and uniform scaling are allowed.
- Retain all 16 frames and unchanged source PNGs. Add no runtime bitmap files or dependencies.
- Run native UI only through the private-desktop launcher. No visible test windows, OS input injection, display changes or live installation.
- Preserve 70% background, 40% unselected, ACTIVE tab, existing controls and keyboard behavior.
- Reuse the current `codex/stopwatch-overlay` checkout and preserve unrelated dirty files. No commit/push/install requested.
- Sharing resources must not create global preference synchronization. The Nika picker uses the default frame and its existing theme surface, independently of overlay frame preferences.

## Review Focus

- Small and scaled overlays: ornaments must fit without exceptions, anisotropic scaling or clipping the time.
- Background masks must follow assembled pieces, including open brackets and Tide Curve waves, and remain behind the border.
- Resizing/theme changes must invalidate cached geometry without unbounded accumulation; timer ticks reuse layouts.
- Picker selection, Escape, Enter, digits/numpad and focus must survive the new wrapper; non-Nika appearance stays as before.
- Nonzero destinations and 1.5x/2x export scale must preserve the frame and content positions.

### Task 1: Shared production layout and mask

**Files:** production `Controls/StopwatchFrameRecipe.cs`, `StopwatchFrameRenderer.cs`, `StopwatchFrameMask.cs`, `StopwatchAppearanceAssets.cs`; harness `SharedFrameChecks.cs` and `Program.cs`.

**Interfaces:** `StopwatchFrameRenderer.GetLayout(StopwatchAsset, Size, double)` returns a reusable layout with minimum size, content insets and background clip; `Draw(DrawingContext, StopwatchAsset, Rect, double)` draws it at a destination. Frame assets carry their catalog ID.

- [x] Add hidden checks that production drawing matches the four reviewed recipes at all three sizes, including nonzero origins; run against the old renderer and observe failure.
- [x] Promote the reviewed recipes, uniformly shrink only when a surface is below its artwork minimum, and build a bounded cache of drawings/clips. Derive mask from assembled artwork once per layout; open brackets use an explicit rounded interior.
- [x] Check all 16 styles, valid small sizes, mask containment, equal-axis scaling, cache reuse and bounds. Build and run `--shared-frames-only` on the private desktop; require no failures.

### Task 2: Overlay and reusable panel integration

**Files:** `StopwatchAppearanceRenderer.cs`, new `StopwatchFrameSurface.cs`, `Views/TimerPickerWindow.cs`, `Views/ControllerWindow.Shortcuts.cs`, native checks.

**Interfaces:** `StopwatchFrameSurface` wraps one child with frame-specific padding, a clipped background and frame artwork; its frame choice is independent of overlay settings. Overlay uses the same cached layout and clips its content to the interior.

- [x] Add checks for overlay dimensions/state, a native picker wrapper and live selection/focus/cancel behavior.
- [x] Replace nine-slice drawing/mapping; use frame-safe insets for overlay name/REC and panel children. Wrap the Nika picker with the default frame without changing list styling or command handling.
- [x] Repoint preview/audit harness to production recipes so future boards exercise shipping code. Run focused hidden frame/appearance/shortcut checks and the existing unit suite.

### Task 3: Review and native evidence

- [x] Export the actual overlay, picker and larger shared panel for review off-screen; inspect backgrounds and borders.
- [x] Check source hashes, no new bundled images, cache bounds and unchanged non-Nika behavior.
- [x] Obtain the execution skill's independent final review; fix material findings and run relevant checks.
- [x] Record results/limitations and leave the build ready for a separate explicit installation request.

## Completion evidence

Completed 2026-11-05. See `design/one-piece-v2/clock-picker/shared-renderer-integration/README.md`. The implementation ledger records the DrawingGroup compatibility ruling, extra-height content ruling, initial picker-focus repair and independent mask-review fix. All required checks passed on private desktops. Physical monitor DPI remains untested. No commit, push or installation performed.
