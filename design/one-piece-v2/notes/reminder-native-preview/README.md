# Add / Edit Reminder — first-layer design

Design-only native Avalonia prototype, rendered on a private Windows desktop. No production editor, installed app, settings, saved reminders, or integration behavior changed.

## Proposed layout

- Reuse Quick Note's parchment/navy palette, shared panel frame and title style.
- Add mode: empty writing area, `Add Reminder` heading and `Save reminder` action.
- Edit mode: synthetic existing text, `Edit Reminder` heading and `Save changes` action.
- Optional `Set date & time` checkbox. Off: hide scheduling fields and show `No date or time set.` On: show native date and 24-hour time pickers with a local-time explanation.
- Keep the writing area the same size when toggling; expand the panel for the scheduling fields. Retain draft date/time values while toggling in the prototype.
- Fixed footer with Cancel and the primary save action; existing Ctrl+Enter / Escape shortcut labels.
- Logical size: 620 × 420 with scheduling off; 620 × 510 with scheduling on.

## Captures and checks

Twelve PNGs cover Add/Edit × scheduling off/on × 70%/90%/125%. Content and the 19 November 2026, 18:30 schedule are synthetic.

The fixture build passed with zero warnings/errors. All twelve native states passed window sizing, draft schedule retention, writing-space preservation, footer separation, and field/button bounds checks. Images were visually inspected, then checkbox spacing and the time-format label were refined and recaptured.

Fixture: `tools/DesktopRegressionChecks/ReminderPreview.cs`; mode: `--reminder-preview`, run only via `Run-HiddenDesktop.ps1`. Logs: `.superpowers/sdd/2026-11-18-reminder-preview/`.

Saving, keyboard dispatch, date/time popup behavior, validation and reminder delivery are not connected or tested in this design prototype. Production implementation awaits design approval.
