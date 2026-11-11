# Activity & Time Review — Feelings & Mood

Native design proposal, continuing the approved shared Navigator frame. Synthetic values; production mood handling, saving and the installed application are unchanged.

## Proposal

- Keep the existing 1.0–10.0 mood scale, 0.1 adjustments and quick scores 1.0, 3.0, 5.0, 7.0, 8.5 and 10.0. The configured default remains respected; the neutral example uses the current code default of 5.0.
- Show the score, existing descriptor and a small vector face together. Navy/parchment controls remain consistent with the other review steps; muted ochre, teal or red-brown distinguish mood bands with readable contrast. No new bitmap assets or animations.
- Keep existing feeling keywords, multiple selection, custom feelings and optional multiline reflection. Selected tags have a navy fill and checkmark. Custom text and notes support Persian/RTL layout.
- Add a compact summary of the period's project time, breaks and gaps, based on the allocation step. Example: 22 project minutes + 3 break minutes + 5 unallocated minutes = 30 minutes. Retained recorded time is included in project time; this does not imply every interval is newly written.
- Keep Back, Save time only and Save & complete fixed below both panes. Saving only time omits the mood entry; saving everything includes score, selected feelings and the optional note.
- Preserve Esc back and contextual Enter behavior: add a custom feeling in its field, insert a newline in the note, and save when the wizard itself handles Enter. Implementation must prevent a tag/preset button's Enter from also saving.
- Both content panes scroll independently at shorter heights; the default 1020 × 700 DIP view fits the demonstrated controls without scrolling.

## Captures

- `neutral.png`: 5.0, no selected feelings, empty optional reflection.
- `selected-feelings.png`: 7.6, Calm/Focused/Motivated selected, reflection entered.
- `low-mood.png`: 2.4, Tired/Overwhelmed selected, reflection entered.
- `custom-feeling-rtl.png`: a selected Persian custom feeling, pending Persian input and RTL note.
- `short-window.png`: 520 DIP height, both content panes scrolled, footer fixed.
- `verification.json`: rendering scope and native layout measurements.

Build the regression fixture and run `--review-mood-preview` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. This drawing-only route does not initialize the production app, settings, hooks or tray. The preview controls have no production action or persistence handlers. Only rendering, default content fit, short-height scrolling and fixed actions were checked.
