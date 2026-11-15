# Internet & Network — Connection & Monitoring implemented

These are native captures of the production SettingsWindow with synthetic probe results, not live network measurements. All checks ran on private Windows desktops. No installed app, user settings, network connection, history or export file was changed.

The approved two-card layout now uses the real controls and actions. Test now shows the last completed check, including latency, download speed, network and check time. It remains usable when automatic monitoring is off, no stopwatch is running, or an interval edit is invalid. Online, slow, unreachable and partial results have distinct feedback; a timeout does not assert that the adapter is disconnected. Leaving the page, changing theme or closing cancels a pending manual check and rejects late results.

Background monitoring is now connected to the desktop controller through the existing InternetMonitorService. It follows the saved enablement, timer-only and interval preferences. The first eligible scheduled check is after 30 seconds, then every 1–120 minutes. Automatic results retain existing history/export behavior; manual tests remain display-only. Manual and scheduled download samples share one gate so they cannot compete for bandwidth. A valid interval applies on Enter or leaving the field; incomplete and invalid drafts retain the previous schedule.

History & export preserves the existing controls under a working second tab. Its visual redesign is a later layer. No shared frame or clock overlay artwork changed.

## Captures

- `wide-70.png`, `wide-90.png`, `wide-125.png`: 1120 × 720 native windows at the requested page scales.
- Matching `-monitoring.png` captures show the lower card when scrolling is needed. Done remains fixed.
- `compact-*`: 840 × 600 windows at all three scales.
- `not-checked`, `checking`, `waiting-for-timer`, `slow`, `unreachable`, `partial-result`, `long-network`, `large-text`: additional production states. Large text uses 125% page scale and 175% text.

## Verification

- Desktop/native and unit builds: zero warnings or errors.
- Final full suite: 1,103 passed, zero failed/skipped.
- Internet native checks: four groups pass (results/preferences; cancellation/stale responses; layout/theme restoration; controller monitor ownership/shutdown).
- Neighboring ActivityWatch checks: four connection, five export and four review-prompt groups pass.
- Approved design fixture: six scale/window combinations pass after restoring production controls before preview reparenting.
- A fresh final review identified interval-prefix persistence and competing manual/scheduled probes. Both received failing regressions before the fixes; those regressions now pass. No critical or minor findings were reported.
- One earlier full-suite run hit the existing CrashLogger concurrent-write assertion. The next baseline verification passed, and the final suite passes with the review regressions included. No unrelated logging code changed.

Run the native checks with `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-internet-native`, using an existing repository-local log directory. Evidence and logs are recorded in `.superpowers/sdd/2026-11-15-settings-internet-connection/`.

Live network behavior, release packaging and Linux rendering were not exercised. Installation and Git publication remain separate user-requested steps.
