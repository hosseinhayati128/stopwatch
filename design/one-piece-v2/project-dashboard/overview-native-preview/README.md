# Project Time Dashboard — overview layer

Design proposal rendered with real Avalonia controls and the shared modular frame. Synthetic values only; production dashboard behavior and the installed app are unchanged.

## Proposed layout

- Shared engraved frame, parchment surfaces, navy typography and a prominent dark tracked-time card. No new bitmap artwork or stretched frame assets.
- Project selector, Day / 7 days / 30 days / All time buttons, and a readable date range. Previous/next-day controls remain available only in Day mode; future-day navigation stays disabled.
- Keep all five existing metrics: tracked time, value score, sessions, projects and active timers. The time total gets more visual emphasis; the other metrics form a two-by-two group.
- Preserve current calculation meanings: simultaneous intervals are added; sessions include intervals in the selected range; Active now is global across all projects rather than filtered. The card label explicitly states its scope.
- A selected project reveals its editable points-per-hour rate and a separate Delete project action. The existing deletion confirmation must remain when implemented.
- Keep Records, Export to Obsidian and Refresh in the header. Bring the existing Add record action into a fixed footer alongside the update time and local-time indication.
- At 1080 × 620 DIP, the overview fits without scrolling. At 760 × 560, filters and summary cards stack in a scrollable body; header/footer remain fixed. Charts, heatmap and records design remain separate layers.

## Captures

- `all-projects-today.png`: all projects and today's summary.
- `project-seven-days.png`: selected project, score rate and seven-day totals.
- `empty-day.png`: a historical day with no records.
- `project-rtl.png`: long Persian project name.
- `all-time.png`: larger accumulated totals and counts.
- `compact.png` and `compact-summary.png`: top/bottom of the narrow layout.
- `verification.json`: native geometry and preview isolation scope.

Run `--dashboard-overview-preview` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` after building the harness. It uses the drawing-only app and native renderer on a private desktop. No live dashboard, settings, hooks, background refresh, export, delete or save handlers are initialized. Controls demonstrate the proposed appearance; production interactions are not implemented or tested by this preview.
