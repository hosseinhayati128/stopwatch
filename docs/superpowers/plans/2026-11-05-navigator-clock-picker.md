# Navigator clock picker implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline and one independent final review.

**Goal:** Apply the selected Navigator row design and state refinements to the actual Nika clock picker.

**Architecture:** Native ListBox items use immutable per-open presentation snapshots, native text/tooltip/accessibility, a small status glyph and scoped item/button styles. Existing selection and controller activation remain authoritative. Reuse StopwatchFrameSurface and existing fonts/resources.

**Tech stack:** Avalonia 11.2.4, .NET 10, private-desktop native harness.

**Spec:** `docs/superpowers/specs/2026-11-05-clock-picker-navigator-design.md`. User's “ok let's continue” resumes this design after the performance fix/publication detour; no further design choice is outstanding.

## Constraints

- No live-app replacement, Git publication, new images/fonts, global appearance settings or timer refresh loop in this implementation step.
- All UI and unit verification runs off-screen. Preserve the six unrelated Todo screenshot changes and current checkout.
- Preserve non-Nika picker layout, timer order/identities, activation, selection, command shortcuts, snapshot semantics and the zero-timer guard.
- Default 460 × 480 and minimum 360 × 300 Nika window sizes. Names ellipsize before complete times and ACTIVE; long hours do not wrap.

## Review focus

- Virtualized/recycled rows must retain their correct name, timer identity, tooltip, mode and ACTIVE badge.
- Hover, selection and ACTIVE are independent, including active plus selected and hover on another row.
- Arrows scroll to positions 10+; digits/numpad 1–9 retain absolute positions after scrolling. Enter and mouse confirmation activate exactly once; close/Escape cancel.
- Long/RTL names, very long hours, four modes, 12+ timers and minimum-size layout remain readable without time/badge overlap.
- Theme-scoped resources, tooltip/accessibility details and button focus/disabled feedback must work without mutating user settings or adding repeated background work.

## Task 1: Native presentation and behavior

**Files:** `Controls/ClockPickerPresentation.cs`, `Views/TimerPickerWindow.cs`, `Views/TimerPickerWindow.Navigator.cs`; `tools/DesktopRegressionChecks/NavigatorPickerChecks.cs`, `Program.cs`.

- [x] Add real-control regressions for Navigator row contents, active/selected separation, long hours/modes/RTL/details, minimum sizing and keyboard/button/double-tap confirmation. Observe missing native presentation RED.
- [x] Build per-open snapshots and native Grid/TextBlock rows with accessible complete details. Add scoped chrome/styles and keep ListBox keyboard/scrolling behavior.
- [x] Use the shared frame and native header/footer/close/select controls. Preserve the legacy branch. Fit complete time text without consuming the name/badge area at minimum width.
- [x] Run focused hidden checks and actual native captures at default/minimum sizes and explicit export scales.

## Task 2: Regression, review and evidence

- [x] Run existing picker/timer shortcuts, theme/state/opacity/performance guard checks and full unit suite on private desktops.
- [x] Inspect actual control captures; obtain one independent final review and fix important findings with a reproducing regression.
- [x] Update design status, retain evidence and leave the local build ready for an explicit later installation/publication decision.

## Completion

Native picker implemented and verified locally. Final private-desktop regression passed 27 groups and 16 timer shortcut subcases; unit suite passed 1,052/1,052. Independent review findings were reproduced RED and corrected GREEN. Native capture inspection additionally found and verified a shared-frame minimum-resize fix. No live installation or Git publication in this step. See `design/one-piece-v2/clock-picker/native-implementation/README.md`.
