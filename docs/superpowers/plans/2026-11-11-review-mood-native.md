# Feelings and Mood Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for the already authorized native execution.

**Goal:** Integrate the approved Feelings & Mood design into the final Activity & Time Review step.

**Architecture:** Reparent the existing mood controls and footer into the shared Navigator frame. Keep score, keyword, note and save handlers; add native presentation helpers and a time summary based on the existing timeline. Other themes and persisted data formats remain unchanged.

**Tech Stack:** C#, Avalonia, existing native regression harness.

**Spec:** `design/one-piece-v2/activity-review/mood-native-preview/README.md`

## Global Constraints

- Reuse the existing frame and fonts; no new bitmap artwork, dependencies or application timers.
- Preserve configured scores/keywords, 0.1 adjustments, six presets, multiple selection, custom feelings and multiline notes.
- Keep Back, Save time only and Save & complete accessible; all testing runs on a private desktop.
- Leave the installed app, Git publication and unrelated Todo images untouched.

## Review Focus

- Keyboard activation of tags, presets and add controls must not also submit the review.
- Tags differing only in case must not duplicate; repeated selection must preserve focus and selected state.
- Back/forward navigation must retain score, selected tags and note while refreshing the time summary.
- Save time only must not emit mood; complete must emit the exact period, rounded score, tags and trimmed note once.
- Long/RTL keywords, many tags and minimum window size must remain usable without clipping the fixed footer.

### Task 1: Integrate and verify the final review step

**Files:**
- Create `StopwatchOverlay.Desktop/Views/PeriodicReviewWindow.Mood.cs` for layout, score/keyword presentation and summary.
- Modify `PeriodicReviewWindow.Navigator.cs` for third-step frame switching and `PeriodicReviewWindow.axaml.cs` for scoped presentation hooks/keyboard routing.
- Create `tools/DesktopRegressionChecks/ReviewMoodNativeChecks.cs`; extend the existing isolated fixture and route in `Program.cs`; update the old allocation test's expected third-step presentation.
- Capture actual controls under `design/one-piece-v2/activity-review/mood-native-implementation/`.

**Interfaces:** Reuse `RenderStep3`, `UpdateMoodScoreDisplay`, `ToggleKeyword`, `AddCustomKeywordFromInput`, `ExecuteSave`, shared `Review*` helpers and existing named XAML controls. New `CreateNavigatorMoodLayout` supplies the third shared-frame page.

- [x] Add/run a failing native check: advancing to mood keeps the shared frame and independently scrollable panes with fixed save actions.
- [x] Integrate approved layout, real period summary, vector score expression and keyboard-accessible feeling buttons.
- [x] Verify defaults/limits, presets, decimal precision, duplicate/RTL/long custom feelings, selection focus, note Enter, back/forward state and save/cancel callbacks.
- [x] Build desktop/harness; run mood and adjacent native suites privately and focused `MoodTests`/`PeriodicReviewTests` without UI.
- [x] Inspect captures and hashes, obtain a final read-only review, fix material findings and show actual native results.

No installation, commit or push is part of this implementation request.
