# Daily Timeline and Records — native implementation

The approved third dashboard layer is integrated below the overview, charts and heatmap on the same scroll page. These captures are rendered from the production `ProjectDashboardWindow`, using synthetic in-memory history on the private Windows desktop. The installed application and personal data are not used by this fixture.

- Full local-day tracks, overlapping lanes, numbered project colors shared with the charts and record rows, running outlines, and small-session markers.
- Newest-first records, 20 per page, Show/Hide and the header Records shortcut, full-source Edit, existing Delete confirmation, fixed Add record footer.
- Partial-period labels and source interval tooltips; past-day portions of open records end at the selected day boundary rather than showing Now.
- Read-only and running-record protections, retained record controls during live ticks, compact rows and RTL/long-name support.
- Timeline day controls are realized near the existing scroll viewport. A 1,200-day synthetic history retains the full scroll extent while creating fewer than 20 day views.

## Evidence

`day.png`, `week.png`, `project.png`, `overnight.png`, `paged-records.png`, `compact.png`, `compact-records.png`, `read-only.png`, `empty.png`.

Expanded captures are 1080 × 720 DIP; compact captures are 760 × 560 DIP. The installed dashboard's default size is unchanged. Content scrolls vertically and header/footer stay fixed.

Run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --dashboard-timeline-native` after building the regression harness. This mode checks real editor save/cancel and delete confirm/cancel callbacks against synthetic data, matching colors/numbers, overlaps, midnight clipping, project filtering, live/read-only actions, paging, retained focus/scroll, bounded history, compact/RTL layout, and the existing charts/overview regressions.

The integration reuses existing UTC/local-day calculations and storage callbacks. It adds no artwork, dependency, configuration, timeline dragging, zoom or new sorting controls. Captures demonstrate the actual renderer and callbacks; installation and user review remain separate steps.

Final validation: all 11 native groups and all 1,052 unit tests passed, with zero failed/skipped tests. The native build has zero warnings/errors. The full unit build has two NU1900 warnings because NuGet vulnerability metadata was unavailable. Fresh read-only code review reported no findings. The final regression also verifies that a running record clipped by the selected period retains both status labels. `verification.json` contains the evidence counts and preservation checks.

## Authorized installation

On 12 November 2026 the Release win-x64 single-file package was installed into `dist/desktop-windows` and launched at the user's request. Installed executable SHA-256: `DCEF41CD526883BD1168C5D360A0182884B888C43F9766F705C1946FAC6A6F6E`. The previous package is retained in `dist/update-backups/dashboard-timeline-20261112-221206`.

Post-restart verification confirmed a single responsive process matching the package, a fresh timer-workspace save, preserved timer identities/names/modes/running states/active selection, no elapsed-time regression, unchanged settings/history, and no new crash logs. The installed panel awaits the user's interactive review; Git publication is separate.
