# Timer Editor Single Close Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for this approved design.

**Goal:** Close a timer from its editor with exactly one confirmation, preserving drafts on cancel and saved history on close.
**Architecture:** Reuse ConfigureCloseTimer with an editor context flag. Extract the existing controller removal body into CompleteCloseSession and invoke it after either route has confirmed. Keep the editor's existing result contract.
**Tech Stack:** C#, Avalonia, native private-desktop fixtures.
**Spec:** design/one-piece-v2/timer-editor/close-timer-native-preview/README.md

## Global Constraints
- All native tests on Run-HiddenDesktop.ps1, never the active desktop.
- No installation, restart, commit or push in this task; preserve approved design artifacts.
- Reuse shared frame; do not change clock overlays or unrelated dialogs.
- Native confirmations: 500 × 380 logical pixels; 70%, 90%, 125%; running, paused, long names.
- Cancel/Esc/X retain the editor draft. Confirm ignores draft edits and preserves project history.

## Review Focus
- Duplicate clicks and owner shutdown must not create orphan confirmations or close a target without consent.
- A timer removed elsewhere or active selection changed must not cause another timer to close.
- Pending record discard and invalid/renamed editor drafts must not leak into the close action.
- Clock/countdown/timecode and legacy themes must retain accurate summary/consequences.
- Other Close Timer entry points still require their own confirmation.

### Task 1: Integrate and verify the single-confirmation flow

**Files:** TimerEditorWindow.axaml/.axaml.cs/.Navigator.cs; ConfirmationDialogWindow.CloseTimer.cs; ControllerWindow.axaml.cs; DesktopRegressionChecks/Program.cs, TimerEditorNativeChecks.cs, new TimerEditorCloseNativeChecks.cs; implemented capture directory.
**Interfaces:** ConfigureCloseTimer(..., bool fromEditor = false); CompleteCloseSession(TimerSession); existing DeleteTimerRequested/WasSaved result remains compatible.

- [x] Add a native real-controller regression which opens the editor, drafts changes, confirms once, and asserts exactly one prompt, target removed, other sessions/history unchanged. Run before production edits: expected failure due to second prompt.
- [x] Extract removal helper; configure editor modal with duplicate guard and owner-close cleanup; use ClockPickerItem snapshot formatting; rename action and add history hint. Add contextual warning and rescale-aware dialog height.
- [x] Verify cancel/button/Esc/X/native close, invalid draft retention, repeat opens, duplicate action, focus/keyboard, running and paused, changed active timer, stale target, visible/hidden owner shutdown, other modes and themes. Render production 3 × 3 matrix with no overflow.
- [x] Build native fixture and test project. Run --timer-editor-close-native, --close-timer-native, --timer-editor-native and --unit-tests only on the private desktop; expected all pass.
- [x] Review production capture sheets, preserve design hashes, and obtain one read-only final review. Record evidence and show the result; do not publish/install.

Ruling: existing feature branch and user-approved design authorize native implementation now; no redundant plan-approval stop. Use PowerShell-equivalent ledger/evidence scripts on Windows; keep evidence for later review.
