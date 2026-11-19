# Keep the shared overlay in place when switching timers

The shared overlay reused one native window but resolved placement from the incoming timer whenever its identity changed. A timer without saved coordinates therefore reapplied the Top Center preset; a previously selected timer could also restore an older position.

The shared window now transfers its actual current coordinates to the incoming timer before placement is resolved. This also handles native moves that have not yet reached the drag callback. The existing workspace fields retain those coordinates. Separate overlay windows keep their own placement; an explicit position-preset change still resets custom positions. Existing screen-boundary clamping remains in place.

## Verification

The regression reproduced a move from `(130, 170)` to `(650, 18)` on timer selection before the fix. Afterward, it retained `(130, 170)` and the subsequent user placement `(240, 260)` across returning/new timers, all timer modes, hide/show and closing the selected timer. It covers Top Center and Custom settings with and without the drag notification, separate-window independence, and explicit preset reset.

All four `--overlay-position-native` groups passed on a private Windows desktop, including existing monitor/DPI placement, mirrored-window retention/cleanup and mode-selection checks. The full unit suite passed 1,117/1,117 with no failures or skips. The native build completed with zero warnings/errors. Evidence is in `.superpowers/sdd/2026-11-19-overlay-switch-position/`.

Native verification used isolated sample state and did not modify live timers or settings. The verified build was subsequently installed for user testing. Physical dragging between multiple monitors was not exercised.
