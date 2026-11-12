# Dashboard charts and heatmap implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Reuse the current feature checkout and preserve unrelated work.

**Goal:** Integrate the approved second dashboard layer, keeping existing analytics and actions.

**Architecture:** Extend the existing Nika dashboard partial layout. Keep the original non-Nika render paths. Reuse the project row controls and year cells, and create native daily-bar controls only for visible dates inside the horizontal scroll viewport. Existing Core calculations remain the source of interval, daily and calendar totals.

**Tech Stack:** C#, Avalonia native controls, shared frame, existing private-desktop runner.

**Spec:** design/one-piece-v2/project-dashboard/charts-native-preview/README.md and approved native captures.

## Global constraints

- Charts follow project/date filters; the heatmap follows project and always covers 53 weeks ending in the current week.
- Preserve overlapping-time addition, local dates/DST, project rate weighting, future-day protection and record actions.
- No new runtime artwork, packages, persistent settings or migrations.
- Long histories must not create one native control per calendar day. Live ticks retain scroll positions and reusable controls.
- Keep tests on private desktops, with synthetic history. Do not install, restart, commit or push in this implementation turn.
- Preserve the approved preview and the six unrelated dirty Todo images.

## Review focus

- Large All time spans, zero days and date-boundary clipping must preserve totals without unbounded native controls.
- Live refresh while reading older dates must preserve horizontal and project-list positions.
- Project colors must remain stable across filters/ranking and match daily bars and existing timeline markers.
- Heatmap selection, future cells, keyboard focus and dates outside the displayed year must remain understandable.
- Compact/RTL layouts, long values, inherited dynamic resources and window closing must not regress the overview.

### Task 1: Native charts and calendar integration

**Files:** new ProjectDashboardWindow.Charts.cs; ProjectDashboardWindow.Navigator.cs; ProjectDashboardWindow.axaml.cs; new DashboardChartsNativeChecks.cs; DesktopRegressionChecks/Program.cs; implementation captures/README.

**Interfaces:** Existing dashboard constructors/callbacks unchanged. Presentation hooks consume DisplayInterval, DayTotal and HeatmapDayValue collections from existing code. Native mode: --dashboard-charts-native.

- [x] Add native regression cases for real project totals/points, zero days, stable project colors, calendar click-to-Day, future dates, compact alignment, long-span bounded controls, retained scroll/control identity and non-Nika compatibility. Run against the current implementation and observe expected failures.
- [x] Implement the approved charts and heatmap as a Nika-only layer below the overview; preserve lower timeline and records layouts.
- [x] Build Desktop/harness; run charts and overview native cases on private desktops, inspect production captures and resolve failures.
- [x] Run the existing full unit suite privately. Review the scoped change once with a fresh read-only reviewer, address material findings and verify preservation hashes.
- [x] Show actual native output in chat; leave source uncommitted and installed app unchanged.
