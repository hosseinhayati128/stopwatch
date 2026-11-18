# Close Timer — first-layer design proposal

A compact native Avalonia design spike for confirming the closure of one timer. It uses the existing shared frame, parchment surface, navy text and muted-red action styling. Standard logical size: 500 × 350.

## Proposed layout

- Navigator heading, short confirmation question and vector X that represents Cancel.
- Pale summary card with a vector stopwatch icon, timer name, current state and elapsed time. The main sample shows Programming, running, at 00:44:27. A paused longer-name sample demonstrates wrapping within the same card.
- Consequence: Stops this timer and removes it from your list.
- Clarification: Saved project records stay in your history.
- Footer with Esc/cancel hint, navy Cancel button and separate pale Close Timer button with muted-red border/text.

The controller currently passes only the timer name to a generic ConfirmationDialogWindow. TimerSessionManager.Close removes that timer and stops its stopwatch; the controller removes its separate overlay and refreshes the timer list. This path does not delete saved project records. The preview uses synthetic values and does not invoke those paths.

## Approach and implementation boundary

The recommended compact summary plus footer pair keeps the target timer and consequence together. Repeating the two large stacked actions from Close Stopwatch would add height to a simple confirmation; a bare one-line prompt would provide less identifying context. This proposal follows the user's approved compact shared-frame direction; its precise layout is awaiting visual approval.

Cancel is visually emphasized. For implementation, initial focus should be on Cancel; Esc and X should cancel, and keyboard activation should follow the focused action without an unfocused Enter accidentally closing the timer. The existing generic dialog has an Enter-to-confirm bubbling handler, so this behavior must be deliberately verified when integrating the design. Do not change other confirmation dialogs or Todo-specific appearance as a side effect.

The summary must use the target timer's current mode/display value. The supplied samples are running/paused stopwatches; clock and countdown labels must use their matching time meaning during integration. The preview time is a fixed sample, not a new live ticker. No new remember preference, countdown, automatic action or history mutation is proposed.

This is a throwaway visual prototype, not production integration. The buttons have no action handlers, and it never loads settings, creates a real controller or timer, or changes the installed application.

## Native captures and checks

Fixture: tools/DesktopRegressionChecks/CloseTimerPreview.cs. Mode: --close-timer-preview, executed only through tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 on a private Windows desktop.

- close-timer-standard-70.png
- close-timer-standard-90.png
- close-timer-standard-125.png
- close-timer-long-name-90.png

All four renders passed checks for native dimensions, text containment, summary/explanation/footer separation and centered action labels. Each image was visually inspected. The fixture build completed with zero warnings/errors. Evidence and pre-existing-work hashes are stored locally in .superpowers/sdd/2026-11-18-close-timer-preview/.

Production keyboard dispatch, timer closure, live data and screen/DPI transitions are not tested by this design spike. No installation, restart, commit or push was performed.
