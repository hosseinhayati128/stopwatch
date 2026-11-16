# Compact Timer Commands implementation

Approved design: `design/one-piece-v2/shortcuts/timer-commands-two-stage-preview`.

1. Add a failing native routing check for six quick actions and second-F2 expansion.
2. Replace the hint presentation with the approved compact layout and lazy full list. Keep native theme colors outside Nika, current bindings, screen bounds and UI scaling.
3. Connect repeated timer leader and unassigned F2 to expansion. Preserve assigned F2, note commands, dismissal and chaining. Refresh active timer context after chained commands.
4. Build and run routing, layout/capture and unit checks on the private desktop at 70%, 90% and 125%. Inspect actual captures and get one final independent review.

Scope: timer hint, controller command-mode routing, native fixtures and evidence. No global-hook redesign, new settings, installation, restart or Git publication.

Ruling: retain the two-second initial timeout; expansion refreshes to five seconds, matching the existing note hint. Chained commands retain the configured timeout. If F2 is assigned, it keeps its action and the hint advertises the configured leader for expansion.

Completed: all four steps. The first native check failed against the old popup, then passed after integration. Native layout checks caught a wrapping Space badge and late text-size coercion; nonwrapping auto-width badges, measured chrome and an explicit existing Controller typography scope resolve both. All scales/custom mappings/text sizes and Midnight/Daylight layouts pass. Unit suite: 1,117 passed, zero failures/skips; both builds have zero warnings/errors. One independent final review was clean. Evidence and capture hashes are in `design/one-piece-v2/shortcuts/timer-commands-implemented/`. No app installation or Git publication performed.

Subsequent user refinements: the popup disappears immediately after a command, C (clock/stopwatch) replaces E in the compact view, and the full list now requires four total presses. The footer counts down 3/2/1 remaining presses; new sessions reset the count. Custom bindings, held-key filtering and Windows chaining remain intact. Updated native behavior and layout checks pass at 70/90/125%. Installation was subsequently authorized and the live build is refreshed through the existing backup/hash/health workflow; receipts are stored separately. Git publication remains pending.
