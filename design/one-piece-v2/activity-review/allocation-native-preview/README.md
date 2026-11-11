# Activity & Time Review — Time allocation & timeline

Native design proposal using synthetic review data. This is a drawing-only fixture; production allocation, saving and the installed application are unchanged.

## Layout and behavior mapping

- Reuses the shared Engraved Filigree renderer, existing fonts and form controls. No new runtime artwork or stretched frame images.
- The summary shows allocated and remaining time, a proportional timeline and the existing collision policy. Manual sessions are navy, stopwatch records teal, breaks brown and unallocated time pale gold. Labels and lock icons also identify their meaning.
- Session cards keep the duration slider, minute input and ±1/±5 controls together. Expanded cards expose exact start/end times, one-minute shifts, ordering, splitting, adding a part and removal.
- Recorded sessions keep their fixed positions and durations. Their details remain expandable; production integration must preserve the existing removal action. The lock means fixed timing, not that the record cannot be removed.
- The chronological timeline makes repeated project sessions and gaps explicit. Existing drag behavior belongs on manual blocks in the summary bar; the fixture does not implement dragging.
- “Split & continue” preserves requested manual minutes around a recorded interval. “Stop at recorded time” caps the manual session at that boundary and explains the minutes left unallocated. These map to the existing `CutAndContinue` and `StopAtFixed` behaviors.
- Breaks and unallocated gaps are excluded from project history. Gaps do not disable saving.
- With feelings enabled, the footer offers Back, Save time only and Continue to feelings. With feelings disabled, it offers Back and Verify & save. The existing Esc/Enter behavior is shown.
- Both content panes scroll independently. Summary and footer stay visible at shorter window heights.

## Captures

- `allocation-overview.png`: 25 of 30 minutes allocated, including a three-minute break and a five-minute gap.
- `exact-time-expanded.png`: manual timing controls expanded, all time allocated, feelings disabled.
- `split-around-recorded.png`: 18 requested Programming minutes split into 10 before and 8 after a fixed ten-minute record; two minutes remain unallocated.
- `stop-at-recorded.png`: the same 18-minute request capped at ten minutes, with the record unchanged and ten minutes unallocated overall.
- `short-window.png`: 520 DIP height, both panes scrolled, actions fixed.
- `verification.json`: native bounds, sample totals and execution scope.

## Reproduction and verification

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then run `--review-allocation-preview` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The early drawing-only route bypasses production app initialization, settings, activity queries and save handlers.

Checked fixture continuity/totals, native control overlap, pane widths, summary/footer bounds, default content fit and independent scrolling at short height. Captures were visually inspected. Event handlers, real activity data, allocation calculations and persistence are outside this design spike.
