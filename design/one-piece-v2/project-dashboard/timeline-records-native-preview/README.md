# Project Time Dashboard — Daily Timeline and Records proposal

Drawing-only native Avalonia previews for the next dashboard layer. Synthetic history, the existing shared engraved frame and native form styles; no production dashboard changes, settings access, record mutations, installation or Git publication.

## Proposed layout

- Keep both sections below the charts and heatmap in the same dashboard scroll area. The header and Add record footer remain fixed. A full-width timeline followed by records preserves room for dates, durations and action labels.
- Each timeline row covers a complete local day, with 00:00, 06:00, 12:00, 18:00 and 24:00 guides. Simultaneous sessions occupy separate lanes. Small sessions retain a visible marker and full tooltip rather than widening the time scale.
- Project colors match the chart palette. Numbered blocks correspond to the numbered record rows; a cross-midnight record can appear on two daily tracks with the same number. Numbers refer to the filtered list, including records on later pages.
- Running segments have an outline; their record shows `RUNNING · pause to edit`, `Now` and `Live`. Closed records keep clearly labeled Edit and Delete buttons. Read-only history suppresses mutation actions and disables Add record.
- Records show project, local time/date, duration within the selected period, and actions. Category/status text sits below the name. Partial records are labeled `Part of saved record`, with a tooltip showing the full saved interval. Edit must continue opening the full original record.
- Keep newest-first order and the existing 20-record page size. Previous/Next sit below the records. Show/Hide records retains the existing collapsed section behavior, and the header's Records shortcut opens that section.
- Narrow windows use a two-line record layout with time and date together below the project, while duration and actions remain visible. The full tooltip retains category and editing guidance. Long names use ellipsis and a full tooltip; Persian names use native RTL text direction. There is no horizontal record-list scrolling.

This proposal keeps existing filtering, overlap addition, local-date/DST calculations, record validation, edit flows and delete confirmation. It does not add timeline dragging, time editing from a bar, zoom, new sorting/filter controls, or persisted project-color assignments. Production implementation would consume the existing interval/day-fragment calculations; the preview uses ordinary synthetic local dates to demonstrate layout only.

## Captures

- `day.png`: today, four records, 4h 5m. Learning overlaps Programming on a separate lane, and the latest Programming session is running.
- `week.png`: three populated days within the selected week, 11h 5m; records collapsed.
- `project.png`: Programming only, 2h 50m, with the same project color on tracks and rows.
- `overnight.png`: records-focused view with a clipped overnight record and a two-minute session. The selected day contains 30 minutes of the full one-hour overnight record.
- `many-records.png`, `many-records-scrolled.png`: a 43-record history with 20 rows on the first page, including long English and Persian names.
- `read-only.png`: visible history with editing unavailable and Add record disabled.
- `empty.png`: empty timeline and records guidance.
- `compact.png`, `compact-records.png`: the top and bottom of the 760 × 560 DIP layout.

Expanded captures use 1080 × 720 DIP. These are preview sizes; the installed dashboard's default dimensions are unchanged. At smaller heights the content scrolls vertically.

## Native preview checks

Run the existing private-desktop harness with `--dashboard-timeline-preview`. Its minimal drawing checks cover header separation, timeline bounds, non-overlapping lanes, matching timeline/record colors and numbers, equal totals, midnight clipping, tiny sessions, running/read-only action states, 20-row page bounds, RTL direction, usable name widths, lack of horizontal overflow and the fixed footer. `verification.json` records native dimensions and fixture totals.

All eight fixture states passed on 12 November 2026. The harness build had zero warnings/errors. Visual inspection tightened the compact rows and added a checked gap between long/RTL names and the time column. The installed executable and six unrelated Todo images retain their prior hashes; production source is unchanged.

Show/Hide and the header Records jump demonstrate local layout behavior only. Edit/Delete, Export, Refresh and pagination are presentation states without production handlers. These captures do not validate persistence or live behavior; those belong to the implementation stage after design approval.
