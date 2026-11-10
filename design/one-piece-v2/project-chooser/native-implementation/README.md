# New Timer / Choose Project — native integration

The approved design now uses the production `TimerNameWindow` controls and handlers. Both New Timer and Set Project supply the current theme; Nika uses the existing Engraved Filigree frame, parchment palette, Navigator heading and Inter body. Other themes retain their original layout. No new runtime artwork, packages, settings or refresh loop were added.

Width is 560 DIP. The default height is 490 DIP, increasing by 112 DIP for each open name editor. Expansion is constrained to the current monitor work area and moves the window upward if needed. The header and actions stay fixed while the form scrolls. Existing project/category/score, unnamed timer, validation, cancellation and keyboard behavior are retained. Persisted score values are bounded before decimal conversion; selecting a project ends stale project/category drafts and loads its saved defaults, including when its category matches the current selection.

Buttons and fields share the existing Timer Editor styles. Buttons include hover, pressed, focus and disabled states. Names support automatic Persian/Latin text direction; errors use inline messages and field borders. Enter selects an open dropdown item without accepting the whole form. Escape backs out of category/project drafts before closing.

These PNGs are actual Avalonia renders of the production dialog with synthetic fixture data:

- `new-timer-existing.png`: existing project, category and score.
- `new-timer-new-project.png`: expanded new-project form.
- `new-category-validation.png`: both editors open, category validation shown.
- `rtl-project.png`: Persian project entry.
- `short-window.png`: constrained height with fixed footer and scrollable fields.
- `choose-project.png`: Apply project action for the current timer.

Reproduce by building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj` and running `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--project-chooser-native'` with a repository-local log path. This uses a private Windows desktop without switching the active screen. Fixtures do not save live projects, settings or timer history.

Verification: six chooser feature groups and five hook groups; Timer Editor native regression; complete shortcut regression; 1,052 unit tests. An independent review identified the same-category draft and screen-position cases; both received failing reproductions followed by passing checks. Build/test receipts and guarded delivery hashes are under `.superpowers/sdd/2026-11-10-project-chooser-native/`. The unit build reported an unavailable NuGet vulnerability-data endpoint; compilation and tests passed. Physical monitor/DPI switching and an interactive live-user session were not tested.
