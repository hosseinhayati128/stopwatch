# Project Time Dashboard — implemented charts and heatmap

Native captures of the production `ProjectDashboardWindow`, using synthetic in-memory history on a private Windows desktop. This is the approved second layer below the overview, inside the same scrolling body and shared engraved frame.

- Ranked project bars show tracked duration and weighted points. Colors remain stable while filtering, ranking and refreshing, and match selected-project daily bars and the existing timeline markers.
- Daily totals preserve zero-time dates. Longer periods scroll horizontally at a readable date width, initially showing the latest dates. Only viewport dates create native controls; a ten-year span remains below 90 plot children.
- The 53-week heatmap follows the project filter independently of the selected period. Green intensity is relative to its busiest day. Today is outlined in ochre, the selected day in navy, and future dates are disabled. Mouse or keyboard activation uses the existing Day filter and updates both charts.
- Live refresh reuses project rows and calendar cells, preserves reading position, and accounts for rolling date origins at midnight/Monday. A view already at the newest dates continues following the newest dates.
- Narrow windows stack the charts. Calendar cells stay fixed-size with horizontal scrolling and stationary weekday labels. The header and Add record footer remain fixed.

## Captures

- `all-projects.png`: 5–11 November 2026, 21h 42m total.
- `selected-project.png`: Programming, 9h 24m and 18.8 weighted points; matching project/daily colors and a zero-time Monday.
- `selected-day.png`: 8 November, 4h 30m across four sessions, with the selected calendar cell and day summary.
- `thirty-days.png`: horizontally scrolled daily totals.
- `project-rtl.png`: Persian project name and automatic text direction.
- `compact.png`, `compact-heatmap.png`: stacked charts and scrolled calendar at 760 × 560 DIP.
- `empty.png`: empty-period/year explanations.

Expanded captures use 1080 × 720 DIP to show the layer together. The default dashboard window remains 1080 × 620 DIP.

## Verification

`tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--dashboard-charts-native'` checks the real production window: project and overlapping daily totals, score edits, project colors, zero days, mouse/keyboard calendar selection, future-date protection, bounded long-history controls, retained row/cell identity and scroll positions, midnight/Monday/rolling-month behavior, compact geometry, RTL, empty state, other themes, and the existing overview's layout/filter/editing/lifecycle/dialog checks.

`-Mode '--unit-tests'` runs the full existing unit suite on a separate private desktop. `verification.json` records the final results. The native harness builds without warnings/errors; the unit build reports NU1900 because NuGet vulnerability metadata is unreachable, with no compile errors.

No new runtime artwork, packages, persistence fields, or calculation rules. Non-Nika presentation remains on its original path. Existing timeline/record layout is retained; this work is not a benchmark or redesign of those lower sections. The twelve-color palette repeats beyond twelve projects and is stable within a dashboard window, rather than a new persisted global color assignment.

The 11 November implementation verification did not install, restart, commit or push anything; its JSON records that state. The six unrelated Todo images were hash-verified unchanged.

On 12 November, the approved Release build was installed and launched separately. Post-launch checks confirmed one responsive app process, all 12 timer identities/names/modes and running flags preserved, unchanged settings, and no new crash logs. The previous executable was retained for rollback.
