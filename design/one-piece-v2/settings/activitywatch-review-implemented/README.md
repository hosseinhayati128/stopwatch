# ActivityWatch Review Prompts — implemented native page

The approved Prompt timing, Activity detail, Prompt example and Review on demand cards now use the production Settings controls. The other ActivityWatch tabs remain active. The layout stacks in compact windows and retains the fixed Done button. Numeric fields grow with the user's text size. Changing the review interval uses the final entered value; reopening the controller retains the existing schedule.

Automatic prompting remains off by default. When enabled, the desktop controller owns a single deadline timer and marshals prompt requests to the UI thread. Review opens Activity & Time Review; Skip waits for the normal interval; timeout or closing the prompt uses the snooze interval. Open dialogs defer automatic prompts. Manual review replaces an existing prompt and remains available with automatic prompts or ActivityWatch off. Disabling, replacing or closing the controller invalidates queued requests and releases the timer/window.

The embedded example only changes explanatory text. It never opens the actual prompt or writes time records. The shortcut hint follows the configured command binding. Valid ranges are enforced before committing settings; invalid timing does not block manual review, while invalid activity detail does. Zero duration retains the existing effective one-second minimum, and zero grouping shows activities separately. Fractional grouping values survive reload without rounding.

## Verification

The full unit suite passed 1,097/1,097. All four Review Prompts native groups passed. Connection & Status (4 groups), Sync & Export (5 groups), and the approved preview (6 sizes) also passed. Unit and native builds completed with zero warnings/errors.

A controlled due-timer interleaving regression also passes: reconfiguring Settings cannot strand a pending prompt. The fresh final review reported no other Critical/Important findings or deferred minors.

All executable checks use `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` on a private Windows desktop. Captures use sample settings, with no changes to the installed app, settings, timer records or external services.

- `--unit-tests`: scheduler deadlines, stale callbacks, lifecycle, normalization and existing core/legacy regression suite.
- `--settings-activitywatch-review-native`: real timer-to-UI delivery; one prompt/review at a time; manual review, modal deferral, skip/timeout/keyboard, cancellation, replacement and shutdown; production controls/validation/examples/shortcut; legacy-theme restoration; six regular/compact captures at 70/90/125%, plus 175% text and maximum four-digit inputs.
- Adjacent checks: `--settings-activitywatch-native`, `--settings-activitywatch-export-native`, `--settings-activitywatch-review-preview`.

Representative captures: `enabled.png`, `default-off.png`, `skip-example.png`, `invalid-timing.png`, `minimum-filters.png`, `enabled-90-compact.png`, `large-text.png`, `large-text-example.png`.

Installed and launched on 2026-11-15 at the user's request. The installed executable matches the verified release; startup, settings, all 12 timer identities/names/modes/running states and the active timer were preserved. Post-launch health found one responsive process, a fresh timer snapshot and no new crash logs. Automatic prompts remain off as configured. Windows private-desktop behavior is covered; Linux rendering and a live scheduled review in the user's session have not been exercised.
