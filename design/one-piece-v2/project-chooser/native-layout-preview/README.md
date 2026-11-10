# New Timer / Choose Project — native design proposal

First layout layer for review, not production integration. The user requested native previews kept off the active screen.

- `new-timer-existing.png`: New Timer with an existing project, category and score; Create timer action.
- `new-timer-new-project.png`: the same form with the new-project name card expanded; Back returns to selection.
- `choose-project.png`: assigning a project to the current timer; Apply project action.

The proposal reuses the production Engraved Filigree `StopwatchFrameSurface` at .16 artwork scale, parchment, Navigator heading font and Inter body text. Width is 560 DIP. The normal panel is 490 DIP high; expanding the project-name card uses 602 DIP. Header and footer stay outside the scrollable form. Ornaments retain their proportions; no new frame bitmap or other runtime artwork is required.

Current project/category selection, score per hour, new-project/new-category entry and unnamed timers remain in scope. Proposed copy changes: label the empty choice “No project” instead of “Select a project”, and use labeled New/Back actions instead of ambiguous plus/close symbols. Selecting an existing project still supplies its saved category and score. The added explanation card makes that existing behavior visible. Escape in the new-project state goes back before closing, matching the existing interaction.

Rendering uses real Avalonia ComboBox, TextBox, NumericUpDown and Button controls, native text/layout and the shared production frame. The controls have synthetic fixture values and no application/save handlers. Button templates are for this visual spike; production hover/focus, validation, expanded new-category entry, long/RTL names, dropdown behavior and keyboard integration still need their own design/implementation checks.

Reproduce by building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj` and running `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode '--project-chooser-preview'` with a repository-local log path. This early harness mode uses drawing services only; it does not start the application's settings/history/hotkey lifetime. All windows stay on the private desktop, which is never switched onto the active screen.

Minimal preview checks verify complete default-height content, field widths, visible fixed actions, and scrolling at 420 DIP height. Measurements are recorded in `verification.json`; build/native logs are in `.superpowers/sdd/2026-11-10-project-chooser-preview/`. The installed executable is unchanged. No save/delete/input-behavior verification or full application regression suite is claimed for this design spike.
