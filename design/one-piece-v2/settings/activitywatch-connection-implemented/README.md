# ActivityWatch Connection & Status — implemented

Native captures of the production SettingsWindow, using a sample HTTP responder and launcher. No real ActivityWatch request, process start or user settings write.

The Connection card keeps integration enablement separate from server reachability. Test shows the actual client response, server version and local check time. Start is available for loopback addresses; missing/unopenable apps and launched-but-unresponsive servers receive separate guidance. Address edits, leaving the page/tab, theme changes and closing cancel obsolete checks. Tests and startup retries are bounded; there is no recurring server polling.

Sync & export and Review prompts preserve the existing controls and callbacks under working tabs. Their visual redesign is a later layer.

Captures: setup, connected, checking, unreachable, app-not-found, server-not-ready; compact-connection / compact-status; connected-70 / connected-125; large-text (125% page, 175% text).

Verification: --settings-activitywatch-native on Run-HiddenDesktop.ps1. All four groups pass: settings/tabs/themes; real client response handling and URL/launch guards; duplicate/stale/canceled operations; layout at 70%, 90%, 125%, normal and compact windows, plus enlarged text. Done remains outside the scroll area.

Final validation: native build has zero warnings/errors; 1,053 unit tests pass with none failed/skipped. Shared Settings regressions (4 groups), neighboring Habitica recovery checks (3 groups) and the archived preview (6 scale/window combinations) pass privately. Fresh final review led to preserving the actual server error after exhausted startup retries; the regression test failed before the fix and passes after it. Live launch/network and Linux were not exercised. Existing export/review algorithms, forms and shared client JSON/URL semantics were preserved.
