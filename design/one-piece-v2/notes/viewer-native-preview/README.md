# Notes, Todos & Reminders — first-layer design

Isolated native Avalonia prototype for the Win+F3, V viewer. No production window, saved notes, completion state, settings or installed application changed.

## Proposal

- Existing shared panel frame and parchment/navy styling, matching Quick Note and Reminder.
- Title, vector close control and compact auto-close indicator.
- All / Todos / Notes / Reminders filter tabs with counts and a clear navy selected state. Subtitle reports filtered versus total entries.
- Date-grouped rows with creation timestamps, type labels, Todo/Reminder completion checkboxes and a vector document marker for Notes.
- Completed text is muted and struck through; in-progress Todos use a teal status label. Reminder rows surface their existing due-date information.
- Fixed footer keeps the existing Enter/Escape close hint and a shortened vault location visible while the list scrolls.
- Filter-empty and unconfigured-vault states provide concise explanations.
- Logical size: 760 × 580. Main previews: 532 × 406 (70%), 684 × 522 (90%), 950 × 725 (125%).

## Evidence

Eight captures: All at 70/90/125%, plus Todos, Notes (including Persian text), Reminders, empty Reminders and unconfigured vault at 90%.

Fixture build passed with zero warnings/errors. All eight private-desktop states passed native size, title/tab/countdown bounds, filter counts, empty-state visibility, scrolling and fixed-footer checks. Captures were visually inspected; filtered counts were refined and recaptured.

Fixture: `tools/DesktopRegressionChecks/NotesViewerPreview.cs`; mode: `--notes-viewer-preview`, run only with `Run-HiddenDesktop.ps1`. Logs: `.superpowers/sdd/2026-11-18-notes-viewer-preview/`.

All content, vault names and dates are synthetic. The auto-close indicator is a static 30-second example; no countdown is running. Completion writes, context-menu actions, keyboard routing, sync and live data loading are not connected or tested. Detailed row actions remain a later layer; implementation awaits design approval.
