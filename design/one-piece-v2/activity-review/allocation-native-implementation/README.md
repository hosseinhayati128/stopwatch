# Activity & Time Review — implemented allocation step

These captures use the actual `PeriodicReviewWindow` with synthetic data and isolated callbacks, rendered on a private Windows desktop. The running installation is unchanged.

The Navigator theme now uses the approved shared frame for time allocation, with session controls, chronological timeline, collision selector and fixed footer. Duration, exact-time, shift, order, split, add and remove controls retain their existing handlers. Optional feelings navigation and saving with gaps remain available. Other themes retain their existing layout.

Recorded intervals cannot be moved through exact-time or gap-drag handlers. Repacking refreshes continuation cards when the selected item list changes. Card replacement suppresses stale focus-loss commits so a focused minute entry is not applied twice. Enter on controls does not also advance or save the review.

Captures:

- `allocation-overview.png`: sessions, recorded interval, break and remaining gap.
- `exact-time-expanded.png`: exact times and secondary actions.
- `split-around-recorded.png`: 18 manual minutes split around the recorded interval.
- `stop-at-recorded.png`: manual time capped at the recorded boundary.
- `short-window.png`: 850 × 520 DIP window, independent scrolling and fixed actions.
- `rtl-zero-minute.png`: long Persian name and zero-minute session.

Reproduce by building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then running `--review-allocation-native` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The fixture uses the drawing-only application and does not initialize live settings, global hooks or the tray.

The native suite checks layout, real action handlers, focused minute entry, split/stop behavior, recorded removal results, manual dragging, optional feelings, keyboard navigation, save callback contents, RTL and other themes. The existing project-step suite and 40 `PeriodicReviewTests` cover adjacent behavior. This is not a live installation or production-history write test.
