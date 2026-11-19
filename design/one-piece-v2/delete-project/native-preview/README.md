# Delete Project — native design proposal

Design-only Avalonia previews for Project Time Dashboard → Delete project, using the existing shared panel frame, parchment, Navigator heading, vector X and centered action labels. Logical size: 540 × 420. No new frame artwork or dependencies.

## Proposed layout

- Header explains the decision; one cream card identifies the project with a wrapping name and an explicit “Time records / All sessions” row.
- The consequence says “Permanently deletes this project and all its recorded sessions.” The separate muted-red sentence says “This cannot be undone.”
- Fixed footer: Esc/cancel hint, navy Cancel with initial focus, and pale Delete project with muted-red text and border.
- The active-timer outcome uses “Cannot Delete Project”, the same project card with amber “Running” status, and instructions to pause or stop the timer before trying again. “Nothing has been deleted” confirms the outcome. There is one navy Got it action with initial focus and an Esc/close hint.

This compact card follows the approved Delete Time Record visual language with less height because there is no individual session interval. A text-only message would obscure project identity; extra statistics, a typed-name confirmation and a stop-timer action are unnecessary for this first layer. The design remains subject to visual approval.

## Preview matrix

| Case | Sample |
| --- | --- |
| Standard | Programming |
| Long name | Programming · Desktop interface improvements and accessibility review |
| Active timer | The same long project name, with deletion blocked by a running timer |

All three states are shown at 70%, 90%, and 125%: native client sizes 378 × 294, 486 × 378 and 675 × 525 in the capture environment. Nine individual PNGs use `delete-project-{standard,long-name,active-timer}-{70,90,125}.png`. Three comparison sheets arrange the corresponding native captures left to right at their original pixel dimensions. Sheet backgrounds and scale labels sit outside the dialog.

## Prototype and verification

`tools/DesktopRegressionChecks/DeleteProjectPreview.cs`, registered as `--delete-project-preview`, creates throwaway controls with synthetic names. It never constructs the production dashboard, reads settings/history, invokes deletion, or pauses a timer. Buttons have no action handlers.

The fixture was compiled with zero warnings/errors and run only through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` on a private Windows desktop, without switching the active desktop. All nine cases passed checks for native dimensions, text containment, footer separation, safe initial focus, expected actions and centered button labels. All three comparison sheets were visually inspected. Logs and preservation hashes are under `.superpowers/sdd/2026-11-19-delete-project-preview/` locally.

## Future integration boundary

The current dashboard confirms deletion, calls its existing callback, then shows the active-timer warning when it receives `ProjectDeletionStatus.ActiveTimerRunning`. These previews represent that existing outcome; they do not introduce an automatic timer-state check or a stop action. Preserve selected-project identity, all-session deletion scope, history persistence, active-interval protection, and existing not-found refresh behavior.

Production integration must also preserve safe cancellation, keyboard activation and accessible long/RTL names and enlarged typography on constrained displays. Production deletion, persistence, keyboard delivery, other themes and physical monitor/DPI changes were not exercised in this design spike. No app installation, restart or Git publication was performed.
