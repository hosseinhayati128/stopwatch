# Running Clocks — first layout preview

2026-11-09. The user accepted the compact layout with “good, continue”. The layout is shown beneath a stopwatch, with a longer list, and empty. This remains a throwaway native design study; it does not change the application.

**Current visual baseline:** `running-clocks-layout.png`. The user subsequently found `running-clocks-states.png` too compact and preferred the previous image. Preserve the earlier layout's spacing, heading, row proportions and footer placement. The state board is an unselected exploration, not an approved implementation reference; any hover/focus treatment must fit the earlier layout without tightening it.

The existing H panel is 350 DIP wide and shows only running sessions other than wall-clock mode. Selecting a name activates that session; Pause pauses it, and Hide dismisses the panel. Its controller positions it below the active overlay, moving it above when there is insufficient room. Those behaviors remain the intended contract.

The proposal retains the width and a 220 DIP list viewport (five 44 DIP rows). It reuses the production Engraved Filigree frame, uniformly scaled to .14, and the clock picker's Navigator fonts and parchment colors. Time and Pause have reserved space; names trim within their own area. The active name gets the established ACTIVE badge. Header and Hide stay outside the scrolling area.

Builder choices for review: compact vertical list, smaller fixed ornament scale, a two-line active row, and footer placement of Hide. No new frame PNGs, fonts, dependencies or preferences. All screenshots are documentation outputs, not runtime assets.

- `running-clocks-layout.png`: the three conditions on one board, with the real `ThemedClockFace` above the three-timer panel.
- `shelf-three.png`, `shelf-many.png`, `shelf-empty.png`: native-size panel exports.
- `verification.json`: exact dimensions and content-fit measurements.

Reproduce by building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then running `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--running-clocks-preview'` with a repository-local log path. The preview starts the drawing-only Avalonia fixture before application settings or hotkeys initialize. It checks the content area and name/time clearance and renders on a private desktop.

The list, scrollbar and buttons are drawn fixture states. Interaction, hover/focus states, live updates, extreme time values, accessibility and monitor-edge placement are not verified in this layout spike; implementation must preserve native controls and the existing timer/shortcut behavior. No app installation or Git publication in this step.

## Next layer: row and action states

`running-clocks-states.png` preserves the unselected state treatment and readability study. Reproduce with `--running-clocks-state-preview`; use the earlier layout board as the visual reference.

- ACTIVE remains attached to the current timer. A pale name-area wash shows hover; a gold outline shows keyboard focus. Neither state changes the active timer by itself.
- Pause and Hide each have normal, hover, pressed and focus treatments. Native implementation should retain real Buttons, full accessible names/tooltips, Tab/Shift+Tab and Enter/Space behavior, and at least 34 DIP action hit areas around the 26 DIP visual keys.
- Pausing Coding removes only Coding from this running-only list, leaving its recorded time and the other running timers intact. The before/after here uses fixture rows; it is not a live action test. If the active timer is paused, it remains the active session but is absent from this list. Focus after removal should move to the next remaining name control (or Hide when empty), avoiding an accidental second Pause.
- Hide dismisses the shelf without stopping timers. The controller continues to own the existing H shortcut and overlay anchoring.
- Long names ellipsize on one line. Persian names use the existing Segoe UI fallback and right-to-left flow; times stay left-to-right. The complete formatted time retains priority, shrinking from 20 DIP only as needed. The longest fixture uses `256204778:48:05.4`; the preview checks a 14 DIP minimum time size and at least 82 DIP of name space.
- Countdown and timecode samples retain the current controller's time formatting. No wall-clock row, new mode label, per-row format preference, confirmation step or animation is introduced.

These interaction ideas remain proposals; their compact presentation was not accepted. `state-verification.json` records rendering checks, not design approval. Pointer/keyboard input, screen-reader behavior and real pause/focus transitions remain untested until native integration.
