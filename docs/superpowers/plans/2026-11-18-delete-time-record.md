# Delete Time Record Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement inline. Steps use checkbox syntax for tracking.

**Goal:** Integrate the approved Delete Time Record confirmation into the dashboard's existing saved-record deletion flow.

**Architecture:** Retain ProjectRecordDeleteWindow, its bool result and the dashboard callback. Add an appearance partial, supply current AppSettings through an optional constructor/ShowAsync parameter, and reuse named XAML controls. The Nika body scrolls when necessary while heading/actions remain fixed; other themes retain their layout.

**Tech Stack:** C#, Avalonia, shared panel frame/typography/page scale, private Windows desktop native checks.

**Spec:** design/one-piece-v2/delete-time-record/native-preview/README.md and approved native captures.

## Global Constraints

- Nika logical size 540 × 470; full Start/End dates and times, UTC-derived duration, wrapping project name, navy Cancel, muted-red Delete record.
- Cancel initially focused; Escape/X/native close cancel; Enter/Space follow the focused action. Complete only once.
- Keep complete original record identity, open-record rejection and dashboard callback/error handling. No history/settings reads or writes added to the dialog.
- All tests stay on a private desktop using synthetic data. Preserve unrelated pending Activity Review, logo and preview work.
- Do not install, restart, commit or push in this request.

## Review Focus

- Overnight, year-boundary, local/DST and long-duration records must show both actual endpoints and duration from UTC elapsed time, not the clipped day row.
- Repeated actions, native close, owner shutdown and ownerless ShowAsync must finish once without accidental deletion.
- Long/RTL names, 150% typography, live page zoom and constrained/negative-origin work areas must keep identity reachable and actions visible.
- Default and legacy-theme callers must remain usable without loading settings; the live dashboard must pass its actual theme/scale.
- Cancellation must not invoke persistence; confirmation must affect exactly the selected full record and preserve adjacent entries.

### Task 1: Integrate and verify the production dialog

**Files:** Create Views/ProjectRecordDeleteWindow.Appearance.cs and tools/DesktopRegressionChecks/DeleteTimeRecordNativeChecks.cs. Modify ProjectRecordDeleteWindow.axaml/.cs, ProjectDashboardWindow.axaml.cs, DesktopPageAppearance.cs, regression Program.cs and DashboardTimelineNativeChecks.cs. Save captures/documentation under design/one-piece-v2/delete-time-record/implemented/.

**Interfaces:** ProjectRecordDeleteWindow(ProjectWorkIntervalView record, AppSettings? settings = null); ShowAsync(Window? owner, ProjectWorkIntervalView record, AppSettings? settings = null); internal FitDeleteRecordContent(bool resizeWindow = false) and FitDeleteRecordToScreen(Screen screen). Native mode --delete-time-record-native.

- [x] Add meaningful native checks for frame/text/action fit, date/duration summaries, default focus/result behavior, and dashboard full-record identity; run against current implementation and record expected failures.
- [x] Integrate the approved shared-frame layout, locale-aware endpoint display, RTL names, safe completion and page/screen fitting. Supply dashboard settings; retain existing deletion callback and error paths.
- [x] Build with `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true`; run --delete-time-record-native through Run-HiddenDesktop.ps1. Expect all focused groups and existing dashboard action regression to pass.
- [x] Inspect captures from the production dialog at 70/90/125%, long/overnight cases and enlarged text. Run affected dashboard regressions, obtain one independent final review and resolve material findings. Record limitations and pending-work preservation.
