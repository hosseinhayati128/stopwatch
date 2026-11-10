# Timer Editor — first layout proposal

2026-11-09. The user approved the main layout with “Ok good continue”. Preserve its spacing and proportions. The next state layer below is a proposal for review; the production editor is still unchanged.

[View the proposed layout](timer-editor-layout.png)

The 620 × 780 DIP panel reuses the production Engraved Filigree `StopwatchFrameSurface`, uniformly sized ornaments at .16, parchment, and existing Navigator/Inter typography. It has no Windows title bar. Header and action footer stay outside the scrolling body. A second arrangement at 640 DIP height verifies a scrollable body and visible Save action; `verification.json` records measured dimensions.

The main editing order is project, category/score, idle stop, elapsed time, latest segment start, then records. Project and category each retain New/List entry. Time retains its live readout, adjustment summary, quick adjustments and Pause/Resume. The fixed footer retains Cancel, Undo Previous Edit and Save & Apply, with the existing Ctrl+Enter, Esc and Ctrl+Z shortcuts.

Builder proposal for review: Recorded segments and Project records start collapsed to keep the main edit uncluttered. Recorded segments contains the existing breakdown. Project records contains status, Discard from Records and Delete Timer; those actions are retained, not removed. Their expanded presentation is a later design layer. Undo is shown for a fixture where undo is available; implementation must still hide it when unavailable.

Behavior is unchanged. In particular, the current idle-stop checkbox saves its preference immediately; the preview does not promise that every control waits for Save. Time-format tooltips, editing validation, running/paused/countdown/clock states, RTL/long names, focus/hover, destructive actions and their confirmations still need their own design/integration checks.

Rendering uses native Avalonia layout, text, vector marks, ScrollViewer, and the real shared frame. Field/button visuals have static fixture values and no input handlers. This does not launch the application lifetime or access user settings, histories, timers or hotkeys. No new runtime artwork, font, dependency or application code was added, and the installed build was not replaced.

Reproduce: build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--timer-editor-preview' -LogPath '<repository-local log path>'` on its private desktop. The build completed without warnings/errors, and both height/layout checks passed. Receipts: `.superpowers/sdd/2026-11-09-timer-editor-preview/`.

## Next layer: expanded records and editing states

- [Expanded records and invalid duration](timer-editor-states.png): two 620 × 780 panels at the approved scale. The expanded panel is scrolled down; its fixed header/footer stay in place.
- [Button states](button-states.png): normal, hover, pressed, keyboard focus and disabled treatments for primary, secondary and destructive actions. A visible outline identifies keyboard focus without moving content.
- [Pending record removal](records-removal-pending.png): status explains removal happens on Save, Discard is disabled after selection, and Delete remains a separate confirmed operation.
- [Valid time adjustment](time-adjustment.png): a focused time field and a visible adjustment summary.
- [Invalid duration](invalid-duration.png): text explains the error, a red field edge identifies its location, and Save is disabled.

The expanded segments show three fixture intervals totaling 00:44:27. Project records keeps Discard from Records and Delete Timer together but distinguishes their consequences. No new confirmation or action has been introduced. The existing delete confirmation itself remains a later visual/integration check.

These are simulated visual states, not live input/save/delete tests. The footer and expanded content passed layout checks; panel size and an unclipped frame are asserted. Native pixel rounding is allowed within one DIP for the footer position. Generate with `--timer-editor-states`; receipts are under `.superpowers/sdd/2026-11-09-timer-editor-states/`. The original approved layout PNG is preserved.

Integration follow-up: route mouse Save and Ctrl+Enter through the same validation. The current editor disables Save for an invalid segment start, but its keyboard save handler only revalidates total duration. Check both inputs together when wiring the redesigned controls. Also retain idle-stop's immediate preference save, conditional Undo, complete adjustment-summary text, field tooltips, and countdown/clock/RTL behavior.
