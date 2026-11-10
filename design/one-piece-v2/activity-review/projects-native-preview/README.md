# Activity & Time Review — Projects & activity

Native design proposal, using synthetic review data. Production review code and the installed application are unchanged.

## Layout

- Reuses the shared Engraved Filigree renderer at `ArtworkScale=.16`, parchment colors and existing form styles. No new runtime artwork.
- Projects and activity details occupy separate scrolling panes. The project sequence and Continue action remain visible at short heights.
- Clicking a project appends a segment, including repeated projects. Position badges show where each project appears. Each sequence segment has its own remove control.
- Break / Empty remains available alongside projects. New project entry stays at the foot of the project pane.
- Activity rows show duration, share of the complete review period and a proportional bar. Small activities remain grouped using the existing percentage threshold.
- An unavailable ActivityWatch connection has a retry action and preserves manual review. Continue depends on the project sequence, not the activity connection.
- The following step allocates time; the timestamps here illustrate the existing chronological sequence, not a new allocation rule.

## Captures

- `selected.png`: four selected segments, including Programming twice.
- `expanded-activities.png`: details of the small activities group.
- `activity-offline.png`: manual review with activity collection unavailable.
- `empty-selection.png`: no selected segments; Continue disabled.
- `short-window.png`: 520 DIP height, both content panes scrolled, sequence and actions fixed.
- `verification.json`: native layout measurements and scope of the checks.

Generate through `DesktopRegressionChecks --review-projects-preview` using the repository's private-desktop launcher. The drawing-only application avoids production app initialization, user settings, activity queries and save handlers.

This spike verifies rendering and layout only. Project selection, removal, grouping controls, retries and navigation are visual proposals without production event handlers.
