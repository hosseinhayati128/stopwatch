# Dashboard Daily Timeline and Records Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the approved Daily Timeline and Records layer into the Nika Project Time Dashboard.

**Architecture:** Add a presentation partial beside the existing Navigator and Charts partials. Reuse interval clipping, local-day splitting, lane assignment, project colors, record dialogs, validation and mutation callbacks. Retain the visible record controls and realize timeline days only near the existing scroll viewport.

**Tech Stack:** C#, Avalonia, existing native private-desktop regression harness.

**Spec:** design/one-piece-v2/project-dashboard/timeline-records-native-preview/README.md

## Global Constraints

- Native previews and all tests run on the private desktop; no active-screen windows.
- Same shared modular frame, fixed header/footer and existing scroll page; no artwork, dependencies or persistent configuration additions.
- Preserve existing filtering, local-date/DST calculations, overlap addition, record validation and delete confirmation.
- Records remain newest first, 20 per page; numbered bars share record numbers and chart project colors.
- Running and read-only records cannot be edited/deleted. Partial rows show the clipped period but Edit opens the full source interval.
- Preserve other themes, six unrelated Todo PNG changes and the installed application. Installation and Git publication follow the user's separate requests.

## Review Focus

- Identical overlapping intervals must keep distinct record numbers and lanes.
- Open sources clipped to a past day must show that day's boundary, not Now.
- Live refresh must preserve button focus and scroll position, with bounded timeline controls for long histories.
- Paging and filtering must never route Edit/Delete to a stale or different source record.
- Long RTL names and narrow windows must leave duration and actions readable without horizontal scrolling.

### Task 1: Integrate the approved layer and validate real actions

**Files:**
- Create: StopwatchOverlay.Desktop/Views/ProjectDashboardWindow.Timeline.cs
- Modify: StopwatchOverlay.Desktop/Views/ProjectDashboardWindow.Navigator.cs (layer and resize hooks)
- Modify: StopwatchOverlay.Desktop/Views/ProjectDashboardWindow.axaml.cs (Nika routing and sorted record list)
- Create: tools/DesktopRegressionChecks/DashboardTimelineNativeChecks.cs
- Modify: tools/DesktopRegressionChecks/Program.cs (isolated native mode)
- Create: design/one-piece-v2/project-dashboard/timeline-records-native-implementation/ (native evidence and README)

**Interfaces:**
- Consumes: DisplayInterval, SplitByLocalDay, AssignTimelineLanes, GetProjectBrush, SafeCanMutateRecords, EditRecordAsync, DeleteRecordAsync and existing named pagination/empty/warning controls.
- Produces: CreateNavigatorTimelineRecords(), RenderNavigatorTimeline(), RenderNavigatorRecords(), ResizeNavigatorRecords(bool compact); existing public dashboard behavior is unchanged.

- [x] Write native regression checks: matching numbers/colors, overlaps, clipped overnight source editing, live/read-only restrictions, successful Edit/Delete and cancellation, pagination, compact/RTL geometry, empty state, retained controls and bounded long-history rendering.
- [x] Build the harness and run `Run-HiddenDesktop.ps1 -Mode --dashboard-timeline-native`; observe failure for missing approved layer before production changes.
- [x] Implement the Nika partial and routing, retaining the legacy non-Nika rendering.
- [x] Run the native mode including chart/overview regressions and capture actual day/week/project/overnight/read-only/empty/compact/paged states.
- [x] Build and run the full unit suite through the private-desktop runner.
- [x] Get one fresh read-only review of this scoped diff; resolve important findings with a failing check and a passing rerun.
- [x] Verify unchanged installed binary and unrelated Todo images; show results in chat. Leave changes uncommitted for the user's staged update/review/publication workflow.

Result: 11 native groups and 1,052 unit tests passed. Native build: zero warnings/errors. Unit build: two NU1900 warnings from unavailable vulnerability metadata. Fresh review: no findings. Author's final partial/running metadata assertion failed before correction and passed afterward. Production captures and verification are in the implementation evidence directory.
