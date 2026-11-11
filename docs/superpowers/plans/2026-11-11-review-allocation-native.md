# Time Allocation and Timeline Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for the authorized native execution. Steps use checkbox syntax.

**Goal:** Integrate the approved allocation and timeline design into Activity & Time Review.

**Architecture:** Reparent existing step-two controls into the shared Navigator frame. Restyle the existing session controls after their handlers are attached; use the existing allocation model, collision behavior, drag handlers and save callbacks. Other themes and the feelings step retain their existing layouts.

**Tech Stack:** C#, Avalonia, existing native regression fixture.

**Spec:** `design/one-piece-v2/activity-review/allocation-native-preview/README.md`

## Global Constraints

- Reuse modular frame artwork without stretching or new runtime images.
- All native execution uses the private-desktop launcher. Leave the installed app running unchanged.
- Preserve recorded intervals, gaps, repeated sessions, removal, exact times, split, ordering and saving.
- Preserve unrelated dirty Todo images. No commit or push requested.

## Review Focus

- Recorded fields or gap dragging must not move fixed records.
- Collision continuations must refresh the editable cards when the selection list changes.
- Enter on a button or field must not also save or advance the wizard.
- Step 1/2/3 transitions must restore the correct frame, actions and selection.
- Narrow/short windows, long names and RTL labels must remain readable and scrollable.

### Task 1: Integrate and verify step two

**Files:**
- Create `StopwatchOverlay.Desktop/Views/PeriodicReviewWindow.Allocation.cs`: native layout and presentation helpers.
- Modify `PeriodicReviewWindow.Navigator.cs`: shared-frame step switching.
- Modify `PeriodicReviewWindow.axaml.cs`: presentation hooks, fixed-record guards and structural card refresh.
- Create `tools/DesktopRegressionChecks/ReviewAllocationNativeChecks.cs`; reuse the isolated fixture in `ReviewProjectsNativeChecks.cs` and route it in `Program.cs`.
- Capture actual production controls in `design/one-piece-v2/activity-review/allocation-native-implementation/`.

**Interfaces:** Consume existing `RenderAllocatedCards`, `SyncAllViews`, allocation handlers, `Review*` styling helpers and the save callbacks. Produce `CreateNavigatorAllocationLayout`, card/timeline presentation helpers, and native regression checks.

- [x] Add a failing native check: advancing to step two retains the shared frame and named independently scrolling panes.
- [x] Run `--review-allocation-native` on the private desktop; expect failure because the approved step-two frame is absent.
- [x] Integrate the frame, summary, collision selector, cards, timeline and footer, reusing existing action handlers.
- [x] Exercise minute controls, exact times, shifts, ordering, split/add/remove, collision policies, fixed-record guards, keyboard navigation, gaps and save results with isolated callbacks.
- [x] Build the desktop/harness, run allocation and existing project-step native checks privately, and run `PeriodicReviewTests`; expect all passing.
- [x] Inspect production screenshots, verify no overlapping controls at short height, review the scoped diff and preserve installed/excluded-file hashes.
- [x] Obtain a final code review, resolve material findings and provide native screenshots with verification limits.

Execution is already authorized. Keep changes local; installation and Git publication are separate user-directed steps.
