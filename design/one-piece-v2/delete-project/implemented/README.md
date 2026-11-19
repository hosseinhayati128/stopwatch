# Delete Project — implemented

The dashboard now uses the approved confirmation and active-timer warning through an opt-in partial of the existing ConfirmationDialogWindow. The Nika layout uses the shared panel frame at logical size 540 × 420, with a wrapping project name, explicit all-session scope, fixed actions and vector X. Other themes retain their existing shell.

Cancel has initial focus. Enter/Space follow the focused button; unfocused Enter does not confirm. Escape, X, native close and owner shutdown cancel. Completion is guarded against repeated callbacks. The active-timer state has a single Got it action and cannot produce a deletion result, including from a stale hidden-button event.

The dashboard captures the project key/name before awaiting confirmation, suppresses duplicate modals with its existing guard, and calls the existing deletion callback once. A changed selection cannot redirect deletion. The core continues to reject open intervals even if tracking begins after confirmation opens. Acknowledging the warning does not stop tracking or retry deletion. The existing not-found outcome and refresh remain intact.

The body scrolls for enlarged typography and constrained screens while the header/actions remain fixed. Project names use the existing RTL helper. Page zoom resizes the window, and monitor fitting clamps it to the work area. Confirmation buttons stack when their measured width cannot fit on a narrow screen. The status notice uses darker teal for at least 4.5:1 contrast. No new artwork, dependencies, background timers, settings or persistence paths.

## Verification

`--delete-project-native`, run only via Run-HiddenDesktop.ps1 on a private desktop with synthetic in-memory history, checks:

- Standard, long-name and active-timer states at 70%, 90%, 125%; native/frame fit, reachable text, centered actions, initial safe focus and readable hover/pressed states.
- 150% text, Persian project names, live zoom and a constrained negative-origin work area, with scrolling to project identity and warning text.
- 720-pixel portrait work area at 200% system scaling and 125% page zoom: both confirmation actions remain contained and keyboard-accessible; status text retains 4.5:1 contrast at 70%.
- Focus-based Enter/Space, Escape/X/native close, repeated actions, blocked confirmation rejection, owner shutdown and legacy themes.
- Real dashboard routing and ProjectTimeHistory deletion: cancellation, duplicate requests, selection changes, deletion of all target sessions while preserving another project, active and late-started timers, retry after pause, not-found refresh and owner shutdown.

The build passed with zero warnings/errors and all five focused groups passed. Four Close Timer groups (including generic/Todo confirmation checks) and five affected dashboard overview groups also passed privately. Twelve PNGs are captures of production controls using synthetic names. Enlarged-text and constrained-screen examples intentionally show the top of the scrollable summary.

Logs and qualification/preservation evidence are retained locally under `.superpowers/sdd/2026-11-19-delete-project/`. No installation, restart or Git publication. Actual user history, OS keyboard injection, physical monitor transitions, external synchronization and the full regression suite were not exercised.

Independent final review found narrow-screen confirmation clipping and low-contrast status text. Both were reproduced in review-red-native.log and corrected in one fix pass. All five focused groups and nine affected regression groups passed on the final build; no unresolved findings remain.
