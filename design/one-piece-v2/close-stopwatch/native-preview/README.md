# Close Stopwatch — first-layer design proposal

Native Avalonia design spike for the dialog shown when closing the controller with Ask every time enabled. Production close handling, remembered preferences, the installed app and pending Activity Review work are unchanged.

## Proposed layout

Reuse the shared frame and parchment/navy/gold styling, with a compact logical size of 500 × 428. The title and vector close control lead into two full-width action rows, a Remember my choice checkbox and a separate Cancel footer.

- **Minimize to tray** is the navy primary action. A tray icon and centered Enter badge accompany the explanation that timers, overlays and shortcuts stay active.
- **Exit Stopwatch** uses a power icon with muted-red text and border on the pale surface. Its explanation says the app, overlays and shortcuts close. No claim is made about discarding or pausing saved timer state.
- **Remember my choice** starts unchecked. The checked sample explains that the next close will use the action chosen here. The settings hint points to Settings → Application.
- **Cancel / Esc / top-right close** keeps Stopwatch open. Cancel is separate from the two close actions; no countdown or automatic exit is proposed.

Stacked rows give each action room to describe its result. A compact horizontal button pair would save height but separate those descriptions from their actions; a larger settings-style panel would repeat the Application page. The stacked version is the proposed approach and follows the recently approved compact popups.

## Existing behavior and implementation boundary

The real controller already handles Minimize, Exit and Cancel, and saves a remembered action only after a non-cancel result. Reuse those paths during implementation. Cancel or closing the dialog must not persist the checkbox state.

The Enter badge follows the existing window handler's intended minimize default. Implementation must verify that focused controls cannot turn that advertised shortcut into Exit; the current bubbling handler alone does not prove this. Esc must cancel regardless of focus. Native modal-owner behavior, exactly-once completion, duplicate close attempts and controller lifetime belong to implementation verification.

The preview creates independent controls and never instantiates the real close dialog or loads settings. Buttons have no action handlers; checkbox state is supplied by each sample. No real controller is hidden or closed, and no preferences are saved. This is a visual proposal awaiting approval.

## Native previews

Fixture: `tools/DesktopRegressionChecks/CloseStopwatchPreview.cs`; mode: `--close-stopwatch-preview`, run only through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` on a private Windows desktop. Uses the actual shared frame renderer, native controls and existing fonts; no generated artwork or dependencies.

Captures show 70%, 90% and 125%, plus a checked Remember my choice example at 90%. All four passed checks for native dimensions, text containment, two action rows, checkbox state, centered Enter badge and footer separation. Each was visually inspected; the checkbox label spacing was refined before the final captures. Builds passed with zero warnings/errors. Build logs, capture hashes and the pending-work baseline are kept locally under `.superpowers/sdd/2026-11-18-close-stopwatch-preview/`.

Production behavior, keyboard dispatch, accessibility interaction, live shutdown and persistence are not exercised by this design spike. Installation and Git publication are separate requests.
