# Close Timer — implemented first layer

CloseSession now opts the existing ConfirmationDialogWindow into the approved Close Timer presentation. The Nika layout uses the shared frame at logical size 500 × 350, with the timer summary, navy Cancel and muted-red Close Timer. A scrollable body keeps the fixed heading and actions available with larger text; normal 70%, 90% and 125% layouts fit without scrolling.

The summary captures the timer's name, mode/state and the controller-formatted displayed value when opened. Clock uses Current time, countdown uses Remaining, and stopwatch/timecode use Elapsed. RTL names use the existing automatic text-direction helper. There is no additional refresh timer.

Cancel receives initial keyboard focus. Escape and X cancel; Enter/Space activate the focused button. The generic unfocused Enter-to-confirm fallback is disabled only for Close Timer. Its completed result ignores repeated actions. Other confirmations, including the Todo-specific shell, preserve their existing behavior.

The controller retains its existing dialog guard and timer removal flow. Native integration checks exercise visible-controller modal and hidden-controller ownerless paths, cancellation, active timer selection changing while the dialog is open, closure of the target's separate overlay, preserved saved history for the target timer, remaining timer identities and owner shutdown. Core timer/history behavior is unchanged.

## Verification

The focused --close-timer-native fixture ran through Run-HiddenDesktop.ps1 on a private Windows desktop with synthetic settings/timers and no real profile writes. All four groups passed:

- Layout, native/frame fit, initial focus, hover/pressed contrast, long/RTL names, page zoom, 150% text, negative screen coordinates and constrained working area; scroll checks verify both ends are reachable.
- Running/paused stopwatch, clock, running/paused countdown (including overdue values), timecode, long durations and snapshot identity.
- Buttons, focused Enter/Space, unfocused Enter, Escape, X, native close, repeated actions, legacy themes and generic/Todo confirmation compatibility.
- Controller lifecycle, target identity, saved records and separate-overlay cleanup.

Build completed with zero warnings/errors. Eight PNGs here were captured from production controls and visually inspected. Evidence and hashes are retained locally under .superpowers/sdd/2026-11-18-close-timer/.

Installation, restart and Git publication are separate requests. Live profile persistence, OS keyboard delivery and physical mixed-DPI monitor transitions were not exercised by these isolated routed-event/native-window checks. Large-text examples intentionally use vertical scrolling.

Independent review found no Critical/Important issues. A dedicated offset-owner centering assertion remains a minor follow-up; current native tests verify work-area containment and exercise owned dialogs, and the owner-center calculation was reviewed.
