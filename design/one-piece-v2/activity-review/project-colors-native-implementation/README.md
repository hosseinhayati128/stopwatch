# Project colors in Time Allocation & Timeline

Actual production controls rendered on a private desktop with synthetic review data. The installed app and user data remain untouched.

- One shared, case-insensitive project lookup supplies the allocation bar, session number markers, zero-minute pins, and timeline markers/outlines.
- Repeated parts of a project match. Colors remain attached during splitting, reordering, editing and back/forward navigation within the review.
- Recorded and manual sessions of the same project share a color; recorded sessions retain the lock and fixed-time labels.
- Breaks use neutral gray-brown; gaps use pale parchment. Card backgrounds and text labels stay readable.
- Twelve muted colors are assigned as projects first appear in the review; larger reviews reuse the palette. No settings or persistent project data were added.

`project-colors.png` shows Arash, Art, Learning and two Apply sessions. `repeated-project.png` shows the scrolled view of the repeated Apply markers. `recorded-break-gap.png` shows the same Apply color on manual/recorded time alongside neutral break/gap colors.

Validation: `--review-allocation-native` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The new native color check failed before implementation, then passed along with the five existing allocation groups. It checks distinct projects, matching surfaces, repeated/case-normalized names, split/reorder/navigation stability, recorded locks, zero-minute pins and absence of persistence writes.
