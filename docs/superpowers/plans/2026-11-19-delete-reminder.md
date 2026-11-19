# Delete Reminder Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement inline. Steps use checkbox syntax.

**Goal:** Apply the approved Delete Reminder confirmation to the real Notes viewer deletion flow.

**Architecture:** Add a scoped partial to the existing ConfirmationDialogWindow, following Delete Project's frame, typography, page scale, screen fitting and completion handling. Keep the existing Habitica/local mutation path; guard the viewer against duplicate prompts and owner shutdown.

**Tech Stack:** C#, Avalonia, shared frame renderer, private Windows desktop native checks.

**Spec:** `design/one-piece-v2/delete-reminder/README.md`

## Constraints

- Preserve approved 540 × 450 short and 540 × 520 long layouts at 70%, 90%, 125%; complete text remains scrollable.
- Show full schedule in local time or No date or time set; preserve local/Habitica/Telegram scope copy.
- Cancel focused initially; Escape, close icon and owner shutdown cancel. Enter activates only a focused action.
- Preserve generic/Todo/Close Timer/Delete Project behavior and legacy themes.
- All native and unit checks run through Run-HiddenDesktop.ps1. Do not read live notes, send requests, install/restart the app or push Git.

## Review focus

- Very long/RTL text and larger typography: content scrolls with actions accessible.
- Narrow high-DPI work areas: frame fits, actions stack, keyboard remains usable.
- Repeated clicks, stale callbacks and owner shutdown: exactly one prompt/mutation, no delayed deletion.
- Settings changed while prompt open: existing vault/account guard prevents redirecting deletion.
- Cancellation versus confirmed persistence: only the captured reminder is removed; neighbors, due metadata and offline sync intent are preserved.

### Task 1: Integrate and verify

Files: new `Views/ConfirmationDialogWindow.DeleteReminder.cs`; modify `Views/ConfirmationDialogWindow.axaml.cs`, `Views/NotesViewerWindow.axaml.cs`, `DesktopPageAppearance.cs`; new `tools/DesktopRegressionChecks/DeleteReminderNativeChecks.cs`; add `--delete-reminder-native` to Program.cs. Preserve the approved preview fixtures.

- [x] Add native layout, actions, accessibility and viewer-flow checks; run privately and observe expected missing-layout failures.
- [x] Implement `ConfigureDeleteReminder(string reminder, DateTime? dueAt, AppSettings settings)`, scoped completion guards, screen/page fitting and actual viewer integration.
- [x] Build and run `--delete-reminder-native` privately; capture all 12 approved cases plus large-text/narrow-screen states.
- [x] Run affected Notes viewer and existing confirmation actions plus the full unit suite off-screen; inspect captures and diff.
- [x] Obtain a focused final review, resolve material findings and record results. Leave installation and publication for separate user requests.

## Results

Six native check groups and 1,117 unit tests passed, with no failures/skips and zero build warnings/errors. The new UI checks first failed against the old dialog. Review identified a stale-row context issue; a two-vault regression failed before the load-time context guard and passed afterward. Linked-target fixtures also verify persisted deletion intent while sync is paused, retaining neighbor revisions and due dates without invoking a network worker.

Narrow-screen enlarged text uses a smaller decorative heading and stacked actions. Windows clamps a native window off a nonexistent negative-origin monitor to the actual display; native fitting was therefore checked with a positive-origin synthetic high-DPI work area. Physical multi-monitor placement and live remote delivery were not tested.

Evidence: `.superpowers/sdd/2026-11-19-delete-reminder/`; production captures: `design/one-piece-v2/delete-reminder/implemented/`. The verified Release build was subsequently installed for user testing on 19 November 2026.
