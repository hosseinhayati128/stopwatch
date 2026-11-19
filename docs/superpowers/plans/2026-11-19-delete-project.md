# Delete Project Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement inline. Steps use checkbox syntax for tracking.

**Goal:** Integrate the approved Delete Project confirmation and active-timer warning into the existing dashboard flow.

**Architecture:** Reuse ConfirmationDialogWindow with an opt-in DeleteProject partial, named controls and existing bool completion. Keep the history callback and result branches; snapshot project identity before awaiting and reuse the dashboard modal guard. Nika uses the approved shared frame; other themes retain their layout.

**Tech Stack:** C#, Avalonia, existing shared frame/page scale/typography, private-desktop native regression harness.

**Spec:** design/one-piece-v2/delete-project/native-preview/README.md and the nine approved captures.

## Global Constraints

- Logical size 540 × 420; standard/long-name confirmation and active-timer warning at 70%, 90%, 125%.
- Confirmation: project card, Time records / All sessions, permanent-deletion explanation, red irreversibility warning, navy Cancel default, muted-red Delete project.
- Blocked state: Cannot Delete Project, running status, pause/stop instructions, Nothing has been deleted, one navy Got it action. Never stop a timer or retry deletion automatically.
- Fixed heading/footer and scrollable body; wrapping/RTL project names; page zoom and monitor fitting. No overlay/frame artwork, dependencies, settings or scheduler changes.
- Tests use synthetic history on a private Windows desktop. Preserve pending Activity Review/logo work. No installation, restart, commit or push in this request.

## Review Focus

- Project selection changes while confirmation is open must not redirect deletion; repeated invocations must not create duplicate modals or callbacks.
- Active intervals, including a timer started after the confirmation opens, must retain all history and produce a dismiss-only warning.
- Escape, X, native/owner close and initially focused Enter must cancel; blocked actions and repeated events must never produce a deletion result.
- Long/RTL names, 150% typography, live zoom and constrained/negative-origin monitors must preserve reachable content and visible centered actions.
- Generic/Todo/Close Timer confirmations and legacy themes must retain their existing behavior; not-found refresh and dashboard callbacks remain intact.

### Task 1: Integrate and verify both states

**Files:** Create Views/ConfirmationDialogWindow.DeleteProject.cs and tools/DesktopRegressionChecks/DeleteProjectNativeChecks.cs. Modify ConfirmationDialogWindow.axaml.cs, ProjectDashboardWindow.axaml.cs, DesktopPageAppearance.cs and regression Program.cs. Save production captures and documentation under design/one-piece-v2/delete-project/implemented/.

**Interfaces:** internal ConfigureDeleteProject(string project, bool blocked, AppSettings? settings); FitDeleteProjectContent(bool resizeWindow = false); FitDeleteProjectToScreen(Screen screen). Native mode --delete-project-native.

- [x] Add native layout, keyboard/result and real in-memory dashboard deletion checks. Run against current code and record expected missing-frame, unsafe-default or routing failures.
- [x] Implement the approved shared-frame partial, safe focus/completion, blocked-state semantics and screen/page fitting. Route the existing dashboard confirmation and ActiveTimerRunning result through it, retaining the captured key/name and modal guard.
- [x] Build with `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true`; run `Run-HiddenDesktop.ps1 -Mode --delete-project-native`. Expect all five focused groups to pass. Run affected dashboard overview and Close Timer suites privately, preserving pre-existing captures.
- [x] Inspect production captures at all requested scales and enlarged text, get one independent final review, resolve material findings and record verification/preservation evidence.
