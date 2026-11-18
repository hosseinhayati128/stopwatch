# Focus Check-in — implemented first layer

The production Nika popup now uses the approved shared frame, vector icons, numbered choices, project/duration summary and explicit automatic-choice countdown. The controller supplies its current settings. Midnight and Daylight retain their existing layout.

The normal and supplied task-switch states request logical sizes 520 × 538 and 520 × 570, respectively. A scrollable summary/choices area keeps the heading and footer reachable with long project names or enlarged text. The popup is capped and centered within the target monitor's working area, accounting for Windows scaling. Page-scale changes refit the frame to the native client area. No new renderer, dependency or raster artwork was added.

## Behavior

One shared default expression drives the highlighted row, countdown and submitted timeout result. Existing threshold and timeout parameters remain authoritative, including the exact threshold boundary and task switches without project details. The close control skips the check-in.

Existing numeric/numpad choices, S, Enter, Space and Escape remain available. A baseline native check found focused buttons could consume Enter/Space and choose a different result than the advertised automatic choice. The window now handles these keys in the tunnel so the displayed contract holds regardless of button focus.

Task-switch detection is unchanged: the dialog supports supplied switch context, while the current controller still opens normal-pause prompts. This layer does not add automatic switch detection or change history rules.

## Verification

Full native fixture build: zero warnings and errors. `--focus-check-in-native`, run only through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`, passed all four groups:

- Production layouts and captures: normal/switch at 70%, 90%, 125%; longer normal pause; long RTL project with 125% text; live page-scale refit; readable fixed heading/footer and scrollable choices; dark automatic-choice presenter backgrounds in hover and pressed states.
- Small-screen fit: a simulated 1366 × 768 working area with 125% Windows scaling and a top offset; native size/position stays within the area, with a scrolling body and visible footer.
- Actions/lifecycle: four buttons, numeric/numpad keys, S, Enter/Space with button focus, Escape, close control, window close, default threshold boundaries/custom threshold, timeout expiry, live countdown update, missing switch details, exactly-once result callbacks and timer shutdown, classic themes.
- Controller integration: eligible resume opens the styled prompt, queued prompts stay tied to their timers, Escape advances without recording, and the selected reason is recorded for the correct synthetic timer.

Nine captures come from real production windows with synthetic values. They were visually inspected. The long-text and small-screen captures are intentionally scrolled to verify the lower choices remain reachable. Capture filenames identify the state and scale.

Independent review found two material issues: the Fluent pressed fill could remove the automatic row's dark background, and the fixed requested height could exceed a small screen's work area. Both were reproduced in native checks and fixed; the final four-group run passed.

Fixture: `tools/DesktopRegressionChecks/FocusCheckInNativeChecks.cs`; controller coverage reuses `SettingsBehaviorNativeChecks.CountdownAndFocus`. Logs and source-hash evidence are kept locally under `.superpowers/sdd/2026-11-18-focus-check-in/`.

Not exercised: full regression suite, live user history, new task-switch detection, or external services.

The Release build was installed on 2026-11-18. Post-restart checks confirmed one responding app process, the expected executable hash, a fresh workspace, all 12 timers and settings preserved, and no new crash logs.
