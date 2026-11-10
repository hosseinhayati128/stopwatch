# Activity & Time Review — implemented first step

These images are captures of the production `PeriodicReviewWindow`, rendered on a private Windows desktop with synthetic activity/history and memory-only callbacks. They are not the design-spike window.

The Nika theme now uses the shared modular frame, independent project/activity scroll areas, a fixed sequence and footer, vector controls, project-position badges, inline creation, and grouped activity details. Other themes and the allocation/mood layouts retain their existing presentation.

Existing controls invoke real selection, registration, removal, threshold persistence, retry, navigation and save handlers. Recorded stopwatch slots are available before the ActivityWatch request finishes. Retrying preserves selections; repeated retries coalesce; closing cancels the request and discards late results. Sequence removal maps back to the matching project occurrence instead of a global chronological index. The core fixed-slot aggregation rules are unchanged.

## Verification

- Desktop/harness build: no errors or warnings.
- Six native regression groups: passed. Includes long lists, short height, horizontal sequence overflow, RTL, keyboard actions, repeated projects, tracked time, save callbacks, legacy themes, retry and cancellation.
- Existing unit suite: 1,052 passed, zero failed or skipped.
- Unit-test build succeeded with two NU1900 warnings because NuGet vulnerability data was unreachable. This was not a dependency vulnerability audit.
- Fresh read-only code review: no material defects. Deferred minor coverage gap: multiple fixed intervals combined with an additional manual occurrence of the same project are not exercised together by a native fixture; the constituent cases and core allocation suite pass.
- During implementation checks, the installed application and unrelated Todo artwork were hash-checked unchanged.

Captures: `selected.png`, `expanded-activities.png`, `activity-offline.png`, `empty-selection.png`, `short-window.png`.

Reproduce with the `--review-projects-native` mode of `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The fixture does not install or restart the app, change live settings/history, or access a real ActivityWatch server.
