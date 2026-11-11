# Dashboard overview implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Work in the existing feature checkout; preserve unrelated changes.

**Goal:** Integrate the approved native Project Time overview while retaining the existing dashboard data and lower sections.

**Architecture:** Follow the existing Navigator partial-class pattern: reparent the current controls and retain their handlers. Apply the shared frame and scoped styles for Nika only. Keep the existing chart, heatmap, timeline and record controls below the overview in the same scroll viewer.

**Tech Stack:** C#, Avalonia, existing shared renderer and native regression runner.

**Spec:** design/one-piece-v2/project-dashboard/overview-native-preview/README.md and its approved captures.

## Global constraints

- No new bitmap artwork, dependencies, data formats or analytics semantics.
- Preserve score edits, deletion confirmation, exports, range navigation and record actions.
- Native checks run exclusively on a private desktop; fixture history is synthetic.
- Preserve installed app and six unrelated Todo captures; no commit, push or deployment in this task.
- Reuse the existing codex/stopwatch-overlay checkout, which contains the approved preview.

## Review focus

- Live refresh during score typing or an open project selector must not replace the editor/selection.
- Narrow windows and long RTL project names must not hide primary actions.
- Read-only callers must not acquire write access when Add record moves into the footer.
- A pending confirmation or editor must retain its owner and existing cancellation behavior.
- Midnight rollover and selected historical days must keep navigation state correct.

### Task 1: Integrate and verify the native overview

**Files:** ProjectDashboardWindow.axaml.cs; new ProjectDashboardWindow.Navigator.cs; DesktopRegressionChecks/Program.cs; new DashboardOverviewNativeChecks.cs; design/one-piece-v2/project-dashboard/overview-native-implementation evidence.

**Interfaces:** Existing ProjectDashboardWindow constructor callbacks and named controls remain unchanged. Add ApplyNavigatorLayout and UpdateNavigatorSummary private presentation hooks. Regression entry: --dashboard-overview-native.

- [x] Write native regression checks for the shared borderless frame, fixed footer under scrolling, readable score field, narrow layout, unchanged calculated totals, range/project filtering, live-refresh editing stability, read-only behavior and modal cancellation. Run before implementation: expect layout and editing-stability failures.
- [x] Integrate the approved hierarchy with existing controls and handlers; preserve lower dashboard sections and non-Nika appearance.
- [x] Build Desktop and harness; run --dashboard-overview-native on Run-HiddenDesktop.ps1. Expect all native cases to pass and capture real production controls.
- [x] Build unit tests and run --unit-tests on the same private launcher. Expect the existing suite to pass; report any failure by name.
- [x] Review the complete scoped diff, visually inspect actual captures and verify preservation hashes. Leave the implementation uncommitted for the user's existing staged workflow.

## Completion

Five native groups and all 1,052 unit tests passed on private desktops. Desktop/harness builds have zero warnings/errors; the unit build had two NU1900 vulnerability-metadata warnings. A fresh read-only review found no Critical, Important or Minor issues. Real export/deletion persistence, lower-chart redesign and sizes below the existing minimum remain outside this change. Installed executable and unrelated Todo captures retain their previous hashes. No commit, push or deployment occurred.
