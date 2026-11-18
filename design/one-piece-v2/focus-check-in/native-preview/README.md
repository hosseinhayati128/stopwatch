# Focus Check-in — first-layer design proposal

Design-only native Avalonia spike for the popup shown after resuming a paused stopwatch. Production behavior, recorded pauses, settings and the installed application are unchanged.

## Layout

- Compact shared panel frame with parchment, navy, gold and teal styling, matching the recently upgraded panels.
- Display title, vector close control and a short question. Closing means skip, as in the existing popup.
- Summary shows the paused project and pause duration. Task switches add the other project and time spent there.
- Four full-width choices keep the existing order: Break / stepped away, Distraction, Idea / brainstorming, Task switch. Each has a centered number badge, vector icon and short description.
- One navy row marked AUTO identifies the timeout default. The footer explicitly names that choice and the remaining seconds, then explains why it applies.
- Existing keyboard meanings are retained in the proposal: 1–4 choose; Enter/Space accept the default; Esc skips. The existing S shortcut also selects Task switch.

A vertical list follows the current reading and keyboard order and gives descriptions room to wrap. A two-column layout would compress the descriptions; a separate confirmation step would add an action to this brief popup. The proposed direction is the compact vertical list.

## Samples and behavior boundary

The six primary captures show a 3m 12s pause on the synthetic project Programming, at 70%, 90% and 125%. The task-switch samples also show 2m 40s on Learning. A seventh 90% sample shows an 8m 40s pause.

The sample uses the existing default threshold of 5 minutes and timeout of 12 seconds: short normal pauses default to Distraction, longer normal pauses to Break, and task-switch states to Task switch. These are sample values, not new hardcoded production settings. The preview countdown is static and no selection writes data.

The production dialog already accepts task-switch context, but the current `ControllerWindow.Behavior` call supplies only normal-pause arguments. This proposal previews both requested visual states. Detecting or routing task switches would be a separate behavior decision during implementation.

## Native preview

Fixture: `tools/DesktopRegressionChecks/FocusCheckInPreview.cs`; mode: `--focus-check-in-preview`, run only with `Run-HiddenDesktop.ps1` on a private desktop. Captures use the existing native frame renderer and native Avalonia controls. No generated artwork or new production assets are required.

Logical size: 520 × 538 for normal pauses, 520 × 570 for task switches.

The build passed with zero warnings/errors. All seven private-desktop captures passed native window sizing, text bounds, four-choice count, single automatic-choice highlight, centered number badges and footer separation checks. Each capture was visually inspected. The first render exposed a clipped footer; placing the countdown explanation beside the countdown kept the compact window size while making the keyboard hint fully visible.

Verification logs: `.superpowers/sdd/2026-11-18-focus-check-in-preview/`. Main captures: `focus-check-in-normal-{70,90,125}.png` and `focus-check-in-task-switch-{70,90,125}.png`. Additional default-rule example: `focus-check-in-long-pause-90.png`.

This is a visual prototype for review. Live countdown, keyboard dispatch, persistence, task-switch detection and external services are not connected or tested.
