# Add / Edit Reminder — implemented first layer

Actual `NoteEntryWindow` captures from the private Windows desktop, 2026-11-18. The installed app was not replaced or restarted.

The Nika Reminder editor shares Quick Note's frame, header, input and footer styling. Existing date/time controls and save handlers are retained. Scheduling expands the window without squeezing the writing area and retains draft date/time values when toggled off and back on. A saved-local/sync-warning state disables editing and offers Close instead of Cancel.

Default logical size: 620 × 420 unscheduled, 620 × 510 scheduled. Minimum width is 600; minimum height is 400/490 to preserve the native pickers and validation feedback. Todo, Todo detail and non-Nika layouts retain their existing presentation. Shared frame assets and floating overlays are unchanged.

## Verification

- Final build: zero warnings/errors.
- Twelve actual Add/Edit × schedule off/on × 70%/90%/125% native layout checks and captures passed.
- Frame/window fit, toggle draft retention, writing-space preservation, native picker typography and control bounds checked.
- Minimum-size, 125% text, long Persian content, scrolling and save-error visibility checked.
- Local create/edit tests verify scheduling retention, removal and addition without duplicate reminders.
- Empty input, missing date, Ctrl+Enter, plain Enter, cancellation, failed-save retry and saved-warning behavior checked.
- Quick Note/shared-editor behavior and Midnight/Daylight theme isolation checks passed.
- Temporary notes folders only; Telegram and Habitica were disabled. Captures contain synthetic text and dates.
- Final representative captures were visually inspected. `git diff --check` passed.

Run `--reminder-native` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. Logs: `.superpowers/sdd/2026-11-18-reminder/` (`red.log`, `build-final.log`, `native-final.log`).

External sync, real reminder notification delivery, date/time popup interaction and the full regression suite were not exercised. Installation and Git publication were not performed.
