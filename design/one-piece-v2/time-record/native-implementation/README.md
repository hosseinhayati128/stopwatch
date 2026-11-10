# Add / Edit Time Record — native integration

The approved panel now uses the production record editor and its original controls, validation and commit callbacks. Controller Add Record and dashboard Add/Edit supply the current theme. Nika uses the shared Engraved Filigree frame, Navigator/Inter typography and existing form styles; other themes retain their original layout. No new runtime artwork, settings, dependencies or timer were added.

The form is 560 DIP wide, with a 620 DIP default height and a 732 DIP expanded-project height, reduced to the monitor work area when needed. At the tested 150% scale, the expanded form fits at 704 DIP. Header/actions are fixed while fields scroll. Failed validation and commit messages scroll into view even at 440 DIP. New/Back restores the preceding project selection; selecting another project leaves the draft. Dates use the current culture's `dd MMM yyyy` display. Durations include seconds, with a compact multi-day form for long records.

Historical-range, future-time, printable-name and DST validation remain in the shared validator. Unchanged UTC endpoints retain their original precision. Integration also fixes ownerless cancellation after a failed commit falsely reporting acceptance, Escape closing the whole editor while a calendar is open, and invalid typed dates silently reverting to a previously selected date. The picker reports parsing failures through its documented [DateValidationError event](https://docs.avaloniaui.net/api/avalonia/controls/calendardatepicker); the editor clears that invalid selection so saving requires a valid date.

Actual native captures, using synthetic fixture data:

- `add-record.png` and `edit-record.png`: integrated add/edit forms, including an overnight record.
- `new-project.png`: real new-project entry.
- `invalid-range.png`: invalid-range feedback.
- `short-window.png`: visible feedback with the fixed footer at 440 DIP.
- `calendar-popup.png`: the native calendar popup.

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--time-record-native'` with a repository-local log path. This mode initializes a drawing-only application with production styles, bypassing production App.Initialize, its settings loader, tray and application lifetime. Fixture callbacks only retain results in memory. Native windows stay on a private desktop that is never switched onto the active screen.

Nine focused groups cover startup isolation, frame/monitor bounds, fixed footer, add/edit/new-project results, exact UTC preservation, invalid ranges and future dates, RTL entry, commit errors/cancellation, plain-theme compatibility, popup keys, typed dates, long durations, visible short-form errors and real captures. Baseline and independent-review defects received failing reproductions followed by passing checks. Shortcut regression and the 1,052-test unit suite were also run privately. Build/test/review receipts are in `.superpowers/sdd/2026-11-10-time-record-native/`.

Release output was built separately in `dist/desktop-windows-time-record/`, then installed and launched after the user authorized the update. The installer verified executable hashes and retained rollback and workspace backups. Physical monitor transitions, real OS key dispatch and DST transitions in a different system time zone were not exercised.
