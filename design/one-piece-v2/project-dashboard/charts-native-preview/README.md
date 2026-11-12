# Project Time Dashboard — charts and heatmap proposal

Native Avalonia design spike for the second dashboard layer. Synthetic values only. This does not change the production dashboard or the installed app.

## Proposed layout

- Keep the approved shared engraved frame, header and fixed Add record footer. This preview shows the charts below the overview; it does not introduce another window, page or tab.
- Time by project uses ranked horizontal bars with a colored marker, readable duration and a secondary weighted-points value. Longer project lists scroll within the card; long and Persian names retain a tooltip and appropriate text direction.
- Daily totals uses vertical bars with a duration above each bar and a local-date label below. Aggregate totals are navy. A selected project's daily bars use the same color as its project marker and comparison bar.
- The project palette follows the subdued palette used in Activity & Time Review. The proposal keeps each project's color stable across dashboard views; it does not claim that current per-review color assignment is already a global project identity.
- Both charts follow the project and period filters from the overview. Zero-time days stay visible in the date sequence. Longer ranges retain readable bar/label widths and scroll horizontally, initially showing the latest dates.
- The heatmap follows the project filter and always displays the last 12 months (53 Monday-based weeks), independent of the chart period. Its green intensity scale represents tracked time relative to the busiest displayed day. Future cells are disabled and faded; today has an ochre outline and a selected day a navy outline.
- Selecting a heatmap day is proposed to retain the existing behavior: switch the dashboard to that Day range. The selected-day capture shows both charts updated to that date and a short day summary below the map.
- In a narrow window the charts stack. The calendar retains fixed-size cells, horizontal scrolling and weekday labels that stay visible. The dashboard's vertical scrolling leaves the header and Add record footer fixed.

## Captures

- `all-projects.png`: all projects over 5–11 November 2026; project and daily totals both equal 21h 42m.
- `selected-project.png`: Programming, 9h 24m; matching project/daily bar color.
- `selected-day.png`: Sunday 8 November, 4h 30m; the selected calendar day is outlined and the charts use that day.
- `empty.png`: no tracked time in either the period or the displayed year.
- `many-projects.png`, `many-projects-scrolled.png`: ten projects with long and Persian labels.
- `thirty-days.png`: wider date range with readable bars and horizontal scrolling.
- `project-rtl.png`: long Persian project selection.
- `compact.png`, `compact-heatmap.png`: the top and bottom of the stacked 760 × 560 DIP view.
- `verification.json`: native geometry, totals and isolation scope.

The expanded captures use 1080 × 720 DIP to show the entire layer together. The installed dashboard's default size is unchanged; at shorter heights this layer remains vertically scrollable below the overview.

## Verification and limits

The drawing-only app runs through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--dashboard-charts-preview'`. Native checks verify frame bounds, header separation, matching project/daily/calendar sample totals, non-clipped duration labels, bar bounds, 371 calendar cells, disabled future dates, long project/date scrolling, fixed weekday labels aligned with the calendar rows, and footer position. All eight fixture states passed; the preview harness build had zero warnings/errors.

No production settings loader, global hooks, refresh timers or record handlers are initialized. The controls demonstrate proposed appearance and states; live filtering, heatmap selection and persistence are not implemented by this spike. No new runtime artwork or dependencies are added. The existing installed build and unrelated Todo artwork are preserved.
