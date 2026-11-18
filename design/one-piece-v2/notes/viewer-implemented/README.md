# Notes, Todos & Reminders — implemented first layer

Production `NotesViewerWindow`, opened by Win+F3 then V, now uses the approved Nika panel layout.

## Scope

- Shared panel frame, title and vector close control; All / Todos / Notes / Reminders filters with counts.
- Date-grouped rows with creation times, completion checkboxes, note markers, in-progress status and reminder due dates.
- Fixed footer using the configured notes subfolder; filter-empty and missing-vault states.
- Existing completion, in-progress, edit and delete handlers retained. The countdown now says "Auto-close paused" when a context menu opens. Mutation errors have a separate message area.
- Midnight and Daylight keep their existing layout. Overlay rendering and shortcut mappings are unchanged.

## Verification

`dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true` succeeded with zero warnings and errors.

`Run-HiddenDesktop.ps1 -Mode --notes-viewer-native` passed on a separate Windows desktop with no switch to the active desktop. Two grouped checks cover:

- Native dimensions and frame fit at 70%, 90% and 125%; tab and title bounds; filtered counts; scrolling with a fixed footer.
- 125% text size at minimum window size, mutation-error display, empty reminders and missing-vault refresh without stale rows.
- Temporary-vault todo and reminder completion, preserved reminder due date, canceled edit and deletion, countdown advance/pause/expiry, Enter/Escape closing and timer cleanup.
- Existing filtering in Midnight and Daylight.

Nine captures come from the actual production window, using synthetic notes in an isolated temporary vault. They were visually inspected. Main native sizes are 532 × 406, 684 × 522 and 950 × 725. The large-text/error capture is intentionally scrolled.

Fixture: `tools/DesktopRegressionChecks/NotesViewerNativeChecks.cs`. Logs: `.superpowers/sdd/2026-11-18-notes-viewer/`.

The same verified source was published and installed on 2026-11-18. Installed executable hashes matched the release; startup checks found one responding process, a fresh workspace, preserved timer identities/running states and settings, and no new crash logs. Local backup and installation receipts are in `.superpowers/sdd/2026-11-18-notes-viewer-install/` and remain excluded from Git.

Not exercised: external Habitica/Telegram sync, physical global-hotkey input, live changes to page scale, or the full regression suite. The existing shortcut dispatch was checked in source. No dependency, search feature, or new row action was added.
