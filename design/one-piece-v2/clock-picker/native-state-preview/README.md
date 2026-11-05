# Navigator states and edge cases

Navigator (row option 1) is retained after the user's “yea ok, good. continue” response to the recommended option. These additions are proposals rendered with Avalonia and the production shared frame. They are static fixtures, not an implemented picker or an input test.

`states-and-edge-cases.png` compares three 460 × 480 panels:

1. **Row states:** Illustration remains ACTIVE; Coding is hovered; Reading remains the keyboard selection. Hover is a pale wash without an outline. Selection is a gold outline with a warmer fill. Hover does not select or activate a timer. Paused rows remain fully readable.
2. **Names and modes:** an active long name, a 123-hour time, a live clock, a Persian name, a countdown, a timecode and an unnamed timer's fallback name. Names ellipsize within their own bounds. Times remain complete and left-to-right. Small mode labels identify the four modes; Clock uses a clock glyph instead of the paused marker. The RTL fixture uses Segoe UI for its script coverage.
3. **More than nine:** a simulated 12-clock list scrolled to entries 7–12. Rows 7–9 retain shortcut keycaps; 10–12 use plain sequence numbers. Planning shows ACTIVE and selection together. Number shortcuts still target absolute positions 1–9, irrespective of scrolling.

The board also shows proposed normal, hover and pressed button treatments. Full name, running/live/paused state, mode, category and separated status should remain available through a row tooltip and its accessible name when the design is implemented. Tooltip behavior itself is not implemented or tested here.

Existing behavior to retain: opening is suppressed when there are no timers; single-click and arrow keys select; Enter, double-click or Select clock confirms; digits/numpad 1–9 confirm directly; Escape and close cancel. The timecode fixture retains the picker's existing HH:MM:SS presentation; it does not propose frame-rate controls or a new timecode engine.

Verification: the native harness built with zero warnings/errors, exported all three scenarios on a private desktop and the board was visually inspected for all 18 rows, names, times, state markers, clearance, RTL shaping and scrollbar placement. Scrolling, pointer input and keyboard behavior are simulated in this design study and require implementation checks later.

Reproduce using `Run-HiddenDesktop.ps1 -Mode '--clock-picker-state-preview'` with a repository-local log path after building the harness. Outputs are `scenario-01.png` to `scenario-03.png`, the comparison board and `verification.json`. No production picker code, app settings, runtime images, installed build or Git publication changed in this step.
