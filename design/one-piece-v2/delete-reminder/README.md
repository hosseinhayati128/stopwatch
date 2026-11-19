# Delete Reminder — native design proposal

The approved design is now integrated into the reminder deletion flow. The original `native-preview/` captures use synthetic, design-only controls; `implemented/` contains captures of the production dialog using isolated sample state.

## Proposed layout

- Reuse the shared engraved panel frame and parchment/navy palette from Delete Project and Delete Time Record. Keep the window borderless.
- Heading: **Delete Reminder**, followed by **Review the reminder before deleting.**
- A cream summary card shows the reminder text and a separate schedule row. Scheduled reminders show a full date, 24-hour time and local-time label. Unscheduled reminders say **No date or time set**.
- Long text wraps in a bounded scrolling area; the action row stays visible. The long-text samples fit in full.
- Explain the existing scope in three short lines: local reminder deletion, linked Habitica task deletion when sync runs, and existing Telegram reminders remaining unchanged. Finish with **This cannot be undone.**
- Navy **Cancel** receives initial focus. **Delete reminder** is a muted-red secondary action. Escape and the close icon cancel. Enter is intended to activate only the focused button in the eventual implementation.

## Preview cases

Each case is rendered at 70%, 90% and 125% with the actual Avalonia controls and shared frame renderer:

1. Short, scheduled reminder.
2. Short, unscheduled reminder.
3. Long, scheduled reminder.
4. Long, unscheduled reminder.

Logical size: 540 × 450 for short reminders and 540 × 520 for the long-text examples. The wider/longer content must remain bounded by the screen work area when integrated.

## Approach

Extend the compact confirmation style already used by the recently approved dialogs. A large editor-style panel adds space without helping this decision; hiding deletion scope in an expander would make the consequence harder to review.

## Preview reproduction

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj` with `UseSharedDesktopRuntime=true`, then use `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --delete-reminder-preview` with a repository-local log path. Always use the private desktop launcher; do not launch the fixture on the active desktop.

The verified Release build was installed for user testing on 19 November 2026.

## Verification result

The preview build passed with zero warnings/errors. All 12 native cases passed on a private desktop: expected dimensions, complete fixture text, footer separation, centered action labels and initial Cancel focus. All four scale-comparison sheets were visually inspected. The 12 individual captures and four comparison sheets are in `native-preview/`; build/native logs and capture hashes are in `.superpowers/sdd/2026-11-19-delete-reminder-preview/`.

The original preview did not exercise production deletion or integrations.

## Implementation verification

All 12 scheduled/unscheduled, short/long layout cases passed at 70%, 90% and 125%, with captures in `implemented/`. Six native groups cover keyboard completion, Cancel focus, stale callbacks, owner shutdown, text scrolling/RTL, enlarged typography, narrow high-DPI fitting, live page scale, legacy themes, real local deletion and existing Notes viewer/Delete Project behavior.

The viewer guards against duplicate confirmations and refuses stale rows after the notes folder or Habitica context changes. Two temporary-vault regression cases reproduce changed-vault/account selection. Synthetic linked Habitica targets verify that only confirmed local deletion stores a deletion revision while sync is paused; cancellation preserves target and neighbor state. No network worker ran.

The full unit suite passed 1,117/1,117, with zero failures/skips. Both builds completed with zero warnings/errors. Evidence and the final review record are under `.superpowers/sdd/2026-11-19-delete-reminder/`. Screen checks used synthetic work areas; physical multi-monitor placement and live Habitica/Telegram delivery were not exercised.
