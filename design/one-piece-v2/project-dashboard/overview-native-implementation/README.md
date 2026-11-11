# Project Time Dashboard — implemented overview

Native captures of the production `ProjectDashboardWindow`, using synthetic in-memory history on a private Windows desktop. The installed application and real history were not opened or modified by these fixtures.

- Nika uses the existing engraved modular frame, approved project/date filters, prominent tracked-time card and four smaller metrics. Other application themes keep their existing dashboard layout.
- Header Records, Export and Refresh retain their handlers. Add record stays in the fixed footer; charts, heatmap, timeline and records remain accessible below the overview.
- All five metric definitions remain unchanged. Simultaneous timers are added; Active now remains global across projects. The selected project retains its editable points/hour and confirmed deletion action.
- Identical live project lists retain the open selector; live refresh preserves an in-progress score edit. Read-only callers cannot open Add record. Closing stops the existing refresh timer.
- At 760 × 560 DIP the filters and metrics stack; scrolling does not move the footer. Persian project labels select their own text direction.

## Captures

- `all-projects-today.png`: production overview, two overlapping projects, 3h 30m total, 5.5 points, four intervals and two running timers.
- `project-seven-days.png`: Programming, four hours over the selected week, rate 2.0 and eight points; Active now still counts both projects.
- `project-rtl.png`: long Persian project label.
- `compact.png`, `compact-summary.png`: narrow window before/after scrolling.
- `empty-day.png`: selected project with no intervals in the selected period.

## Verification

Desktop/harness build: zero warnings and errors. Five native regression groups passed: standalone layout/contrast/fixed footer, calculations and filters, live editing/lifecycle, existing dialogs/theme compatibility, production captures. The full existing unit suite passed: 1,052 passed, zero failed/skipped. The unit-test build reported NU1900 warnings because NuGet vulnerability metadata was unavailable; compilation and test execution succeeded.

Run the native suite with `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--dashboard-overview-native'`; run the unit suite with `-Mode '--unit-tests'`. Both execute on a private desktop. Build the corresponding projects before running.

Native fixtures exercise the actual windows and handlers with in-memory callbacks. Obsidian export and confirmed destructive deletion were not executed against real data. The lower analytics panels retain their existing design. This task does not deploy or publish the build.

A separate read-only final review found no Critical, Important or Minor issues in the scoped implementation. Installed executable and unrelated Todo artwork hashes were verified unchanged.
