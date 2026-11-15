# Habitica Sync & Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Implement the approved native Habitica Sync & Recovery design using existing integration actions.

**Architecture:** Extend the current navigator partial with a recovery partial; reuse the existing buttons, confirmation and progress controls. Keep the legacy layout and integration semantics. Read local inventory off the UI thread and scope asynchronous results to the current vault/account.

**Tech Stack:** C#, Avalonia, existing native private-desktop regression harness.

**Spec:** design/one-piece-v2/settings/habitica-recovery-native-preview/README.md (approved by the user's implementation request).

## Global Constraints

- Tests and captures run on a private desktop; no installed-app update or live Habitica requests.
- Preserve independent-vault sync, explicit copied-vault transfer, account/vault confirmation guards, token masking and drafts.
- Reuse the shared Settings shell and frame; no clock-overlay or asset changes, no dependencies.
- Continue in the current codex/stopwatch-overlay checkout with the approved preview files preserved. Leave changes reviewable without committing or publishing in this implementation request.

## Review Focus

- Account/vault changes while a confirmation or inventory read is pending must not authorize the previous action.
- Connection drafts and revealed tokens must survive tab switching safely; hidden pages must not retain refresh loops.
- Progress means server acknowledgements, not queued work; pause, blocked, interruption and ownership failures need visible feedback.
- Theme restoration must return every moved control to its legacy parent and retain working handlers.
- Compact windows and enlarged text must preserve actions, scrolling and the fixed Done button.

### Task 1: Implement and verify recovery navigation and actions

**Files:** Modify `HabiticaSettingsPanel.cs`, `.Connection.cs`, `.Navigator.cs` under `StopwatchOverlay.Desktop/Controls`; create `.Recovery.cs`. Extend `tools/DesktopRegressionChecks/SettingsHabiticaNativeChecks.cs`, add `SettingsHabiticaRecoveryChecks.cs`, register in `Program.cs`, adapt the archived recovery preview where needed. Add implemented screenshots and a short README under `design/one-piece-v2/settings/habitica-recovery-implemented`.

**Interfaces:** Consume `CountExisting`, `CountMissing`, `GetStatus`, `ImportExisting`, `RecreateMissing` and `EnableHere` without changing core contracts. Produce navigator tab selection, recovery status rendering and inline confirmation routing through existing guarded callbacks.

- [x] Add native behavior checks for tab navigation/drafts, empty-vault disabling, real queued import after confirmation, recreation/transfer cancel and guarded execution, acknowledgement progress, scope changes, theme restoration and responsive capture.
- [x] Build the fixture and run `Run-HiddenDesktop.ps1 -Mode --settings-habitica-recovery-native`; expect failure because the recovery tab is absent.
- [x] Implement recovery cards/tabs and route existing actions; perform inventory reads asynchronously with stale-result guards; preserve legacy styles/handlers and visible error reporting.
- [x] Run the recovery and existing Habitica native groups privately. Expect all pass, with sample captures from actual production controls.
- [x] Build DesktopRegressionChecks and run the project unit suite; expect zero build errors and all unit tests pass. Run adjacent Settings shell checks if shared-layout behavior changes.
- [x] Obtain one fresh final review; fix material findings with focused regression checks, inspect final captures, and record evidence. Leave implementation uncommitted for the user's normal update/push workflow.
