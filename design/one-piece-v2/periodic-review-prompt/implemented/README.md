# Periodic Activity Review — implemented first layer

The production Nika reminder now uses the approved compact shared frame, local period summary, centered Y/Esc badges, vector icons and separate countdown footer. The controller supplies current settings; other themes retain their existing layout.

## Behavior

- Review now / Y opens Activity & Time Review for the supplied period.
- Skip / Esc retains the regular review interval. Closing or timeout retains the snooze interval and never accepts a review.
- Both intervals and the live countdown reflect the existing configuration. Every dismissal stops the timer and delivers one result after closure.
- Local date labels distinguish today, yesterday and older dates; duration uses UTC endpoints so daylight-saving changes do not inflate or shorten the period.
- The logical size is 480 × 396. Page zoom changes resize the popup to preserve these proportions and recenter it within the monitor work area. The window is capped when necessary, with a scrollable body and fixed heading/footer for larger text or small displays.

No new settings, scheduler, external service request, artwork or dependencies were added. The existing named action controls and handlers are reused.

## Native verification

The fixture build passed with zero warnings/errors. `--periodic-review-prompt-native`, run only through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` on a private Windows desktop, passed four groups:

- Native layouts at 70%, 90%, 125%; custom timing; overnight periods; enlarged text; live zoom; small work area; centered badges and readable primary-action hover/pressed states.
- Today, overnight and older date labels with accurate UTC duration, including a historical local DST transition interval.
- Buttons, focused Y/Esc, close, timeout, live countdown, independent skip/snooze values, callback order/count, timer shutdown, and Midnight/Daylight compatibility.
- Controller settings propagation, skip/snooze scheduling, one review at a time, cancellation, disabling, stale requests, real timer marshalling, and shutdown.

Seven captures show actual production windows with synthetic values. They were visually inspected; enlarged-text and small-screen captures are intentionally scrolled to show both actions remain reachable. `PeriodicReviewPromptNativeChecks.cs` owns the popup checks; controller checks reuse `SettingsActivityWatchReviewNativeChecks.ControllerLifecycle`.

An initial run found too little body space when zoom increased with enlarged text. Resizing the native window with page zoom resolved the failure. Independent review then found incorrect vertical centering and a legacy-theme duration calculation that used local clock readings. Both were reproduced in native checks and corrected locally: placement centers both axes, and every theme uses one UTC-derived duration. The shared overlay placement helper is unchanged.

Logs and source/capture hashes are stored locally under `.superpowers/sdd/2026-11-18-periodic-review-prompt/`.

Not exercised: full regression suite, live user history, external integrations or physical multi-monitor transitions. This build has not been installed or pushed.
