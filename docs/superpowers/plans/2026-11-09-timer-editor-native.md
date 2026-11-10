# Timer Editor Native Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. Layout and state previews are approved; native integration is authorized.

**Goal:** Integrate the approved Nika Timer Editor and make Save/keyboard validation consistent and safe.

**Architecture:** Reuse the existing editor controls, handlers and return values inside the production shared frame. Add a Nika-only presentation partial and scoped styles; preserve other themes. Centralize validation at the save boundary and retain the current duration formats and segment-start calculations.

**Tech Stack:** Avalonia/C#, existing native regression harness and private Windows desktop.

**Spec:** `design/one-piece-v2/timer-editor/native-layout-preview/README.md`, `timer-editor-layout.png`, `timer-editor-states.png`, `button-states.png`.

## Global Constraints

- Preferred 620 × 780 DIP, shared Engraved Filigree at .16 and padding 10; bound initial height to available monitor space. Keep a fixed header/footer and scrollable body, with roomy section spacing.
- Keep project/category New/List entry, score, idle stop, live time, adjustment summary, quick adjustments, segment start/breakdown, pending discard, confirmed deletion, conditional Undo, and save/cancel shortcuts. Idle stop still saves its existing preference immediately.
- Real native inputs/buttons/expanders, focus and readable field errors. No new app preference, runtime artwork, dependency, or refresh timer. Retain and stop the existing 500 ms live refresh timer on close.
- All UI tests run privately; no active-desktop windows/input. Preserve prior Running Clocks work and unrelated Todo assets. No Git publication.

## Review Focus

- Mixed invalid duration/start fields must block both Save and Ctrl+Enter; fixing one must not hide the other's error.
- Oversized and non-finite durations, maximum elapsed values, and quick-adjust arithmetic must not crash input handling.
- Expanded records, long/RTL names, narrow/short windows and actual dropdowns must retain readable input and a visible footer.
- Cancel/close/Undo/Delete/Discard must preserve existing return semantics and never mutate fixture timers prematurely.
- Other themes, all timer modes, hidden-owner opening and closed-window timer cleanup must remain compatible.

### Task 1: Validate and integrate the editor

**Files:** Modify `StopwatchOverlay.Desktop/Views/TimerEditorWindow.axaml` and `.axaml.cs`; add `.Navigator.cs` and narrowly scoped styles if required. Add `tools/DesktopRegressionChecks/TimerEditorNativeChecks.cs` and the `--timer-editor-native` route in `Program.cs`. Save native evidence under `design/one-piece-v2/timer-editor/native-implementation/`.

**Interfaces:** Keep the public constructor, result properties, `ShowForOwnerAsync`, `TryParseDuration` and `FormatDuration`. Reuse existing control names and event handlers; the controller contract remains unchanged.

- [x] Add native regressions for validation/overflow, fixed-footer layout, real fields and buttons, records actions, timer modes, RTL/long values, lifecycle/theme isolation and captures. Build and run the baseline; expect the missing frame and keyboard validation/overflow cases to fail.
- [x] Implement guarded shared validation and the Nika presentation using existing controls. Run the focused checks to green; inspect real captures against the approved layout/state references.
- [x] Run existing hidden-owner/shortcut checks and the complete unit suite. Get one fresh independent review; fix material findings with a failing regression before the fix.
- [x] Publish a separate Release candidate, update the saved app only if closed, retain and verify a rollback copy, and leave the app closed. Keep changes uncommitted for review.

The spike is only a visual reference. Native integration must retain the complete adjustment messages and editor semantics, even where a fixture showed shortened sample text.
