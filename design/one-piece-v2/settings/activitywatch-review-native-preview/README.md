# ActivityWatch — Review Prompts native proposal

Drawing-only Avalonia preview for design approval. Uses the existing Settings shell and shared frame with independent sample controls. No real review prompt, review window, scheduler, saved preferences, history changes or ActivityWatch requests are triggered. The installed app is untouched.

## Proposed arrangement

Keep the ActivityWatch tabs. Put Prompt timing and Activity detail on the left, with an embedded Prompt example and Review on demand on the right. Stack the cards in compact windows and keep Done outside the scroll area.

This follows the approved Connection & Status and Sync & Export layers. A single long form separates the settings from their consequences; a wizard adds steps to recurring preferences. The proposed two-column design keeps an editable example alongside the controls. The user has requested this native design spike; production implementation is a separate approval.

## Controls and meaning

- **Show review prompts:** off by default. Automatic prompts run while Stopwatch is open. Turning them off retains timing values; Review now and activity-detail preferences remain available.
- **Review every:** default 30 minutes; range 1–1,440. The normal review interval also determines the next reminder after Esc/Skip and after a completed review.
- **If ignored, remind after:** default 30 minutes; range 1–1,440. Applies when the prompt closes without a response. It is distinct from the normal interval.
- **Keep prompt visible for:** default 5 seconds; range 1–120. After this time, an unanswered prompt is treated as ignored and snoozed.
- **Short activity threshold:** default 15 seconds; stored range 0–3,600. Current aggregation treats 0 as a 1-second minimum, so the preview explains that instead of claiming filtering is disabled. Filters standalone short activity and joins matching work across brief interruptions.
- **Group activities below:** default 3%; range 0–50, finite decimal values allowed. Smaller summary entries are grouped into Others; 0 shows entries separately. Existing aggregation retains at least the largest activity as context.
- **Review now:** available with automatic prompts off and ActivityWatch integration off. It opens Activity & Time Review; if ActivityWatch is disabled/unavailable, stopwatch time can still be reviewed. Default shortcut is Win+F2, then V; production should display the actual configured binding.

## Embedded example

The example stays inside Settings; it never opens a floating prompt. Y explains opening the review; Esc explains waiting the normal interval; the ignored example explains snoozing. Skipping or ignoring a prompt does not delete recorded time.

The ready example uses an illustrative 14:00–14:30 period. The timing example uses a 45-minute review interval, 10-minute snooze and 8-second display time to make the distinction visible. Actual review coverage starts at the last completed review when it is in the past 24 hours; otherwise it uses the configured review interval. This is not a live next-prompt countdown or persisted review status.

Other tabs are disabled in this standalone preview. Manual and example actions only update explanatory sample text. Invalid timing drafts flag the relevant field without blocking an independent manual review. Invalid detail filters disable the sample Review now action until corrected. Values are sample inputs and are never committed.

## Implementation findings

The Desktop Settings controls and manual review route exist, as do shared PeriodicReviewService and a Desktop PeriodicReviewPromptWindow. The Desktop controller currently does not instantiate/connect that service or prompt; the legacy WPF controller does. Implementing this layer must include bounded scheduler/controller lifecycle, settings refresh, one prompt/review at a time, handling accepted/skipped/ignored/closed outcomes, and cleanup on shutdown. The preview is not evidence that automatic desktop prompts currently work.

Prompt scheduling is independent of ActivityWatchEnabled. ActivityWatchEnabled controls whether external activity is read during review. Keep those meanings separate. Preserve existing stopwatch data, review period selection, keyboard behavior and review completion paths. Fields and callbacks must restore their original sibling ordering in legacy themes.

## Captures and checks

Native regular 1120×720 and compact 840×600 windows at 70%, 90%, 125%, plus 175% text in compact 125%. Checks cover horizontal fit, action captions, fixed Done, keyboard focus, default-off state, retained timing, independent integration/manual availability, invalid inputs, zero thresholds, ignored-versus-skipped examples and no changes to the sample settings/history.

Captures: default-off, enabled, timing-example, skipped-example, ignored-example, manual-example, activitywatch-off, invalid-timing, invalid-detail, minimum-filters, compact-timing, compact-example, enabled-70, enabled-125, large-text.

Run only via tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 with --settings-activitywatch-review-preview. No full application test, live reminder or Linux render is claimed by this design spike.
