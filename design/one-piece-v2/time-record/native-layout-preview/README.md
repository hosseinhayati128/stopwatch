# Add / Edit Time Record — native design proposal

First layout layer for review. These are native Avalonia previews with synthetic data, not production integration. The user's active screen and running app are untouched.

- `add-record.png`: existing project; two date/time columns; 1h 30m duration; Add record action.
- `edit-record.png`: completed record crossing midnight; 2h 15m duration; Save changes action.
- `new-project.png`: expanded project-name entry, using the New/Back treatment from Choose Project.
- `invalid-range.png`: end time before start, field border and inline feedback, duration unavailable.
- `short-window.png`: 440 DIP height, body scrolled to its end, header/actions kept fixed.

The proposal uses the production Engraved Filigree `StopwatchFrameSurface` at .16 artwork scale and padding 10, without stretching ornaments or creating new runtime artwork. Parchment, navy text, Navigator headings, Inter fields and the shared production `NavigatorFormStyles` match the recently approved panels. Native ComboBox, TextBox and CalendarDatePicker controls supply the fields; native button templates supply their visual treatment.

Width is 560 DIP. Default height is 620 DIP; new-project entry uses 732 DIP. The header and footer stay outside the scrolling form. Start and end stay side by side; explicit dates show overnight records. Dates use `dd MMM yyyy` in this English fixture to avoid ambiguous numeric month/day ordering. The duration is the main summary. A New/Back button is proposed in place of the current dropdown's new-project item; this preserves the existing ability to create a project while making it consistent with Choose Project. The edit fixture's overnight explanation is a proposed contextual hint, not a new time-entry mode.

Scope retained from the existing form: add/edit completed records, existing/new project, local date/time entry, total duration, inline validation, Cancel and Escape. Production name limits, valid historical ranges, future-time rejection, daylight-saving handling and original UTC endpoint preservation must remain during any later integration. There are no category, score, quick-duration or new record-management features in this proposal.

Reproduce by building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then using `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--time-record-preview'` with a repository-local log path. The early drawing-only harness route skips the application's settings/history/hotkey lifetime. The private desktop is never switched onto the active screen; no save handlers are attached.

Build passed with no warnings/errors. Minimal layout assertions cover all four normal states, date field widths, frame bounds, complete content, fixed footer and 440 DIP scrolling. Measurements are in `verification.json`; build/native receipts are in `.superpowers/sdd/2026-11-10-time-record-preview/`. Calendar popup, keyboard navigation, validation behavior, save/cancel effects, long/RTL names and monitor/DPI behavior need production-stage checks. The full application suite was not rerun for this visual spike. No installed build was replaced or restarted.
