# Periodic Activity Review — first-layer design proposal

Design-only native Avalonia spike for the reminder that opens Activity & Time Review. The user approved exploring a compact shared-frame popup with two actions and a clear countdown. Production UI, scheduling, stored preferences, history and the installed app are unchanged.

## Layout and approach

Use the existing shared panel frame, parchment/navy/gold/teal palette and vector icons, matching Focus Check-in. Requested logical size: 480 × 396. No new artwork or dependencies.

The proposed layout has a heading and close control, a period summary with duration, two stacked choices, and a separate countdown footer. The navy primary row means the available Review now action; it is not an automatic selection. Centered Y and Esc badges retain the existing shortcuts.

A horizontal pair of buttons would be shorter but leave less room to explain Skip. A larger report-style card would duplicate the review page. The compact stacked layout is the proposed approach: it keeps both actions and their consequences readable without adding steps.

## Existing behavior to preserve

- **Review now / Y:** open Activity & Time Review for the displayed period.
- **Skip this review / Esc:** dismiss and remind after the configured regular review interval.
- **No response / window close:** dismiss and remind after the configured snooze interval. The close control's accessible name and tooltip state this behavior; the footer also names it.
- Timeout seconds, regular interval and snooze interval come from the existing settings. No new defaults or actions are proposed. Skipping or ignoring does not delete recorded time.
- The scheduler/controller lifecycle already exists. The UI work should preserve its accepted/skipped/ignored results, callback-once behavior, timer cleanup, settings changes and suppression while other relevant dialogs are open.

These distinctions matter when the two intervals differ. The default sample uses a 30-minute interval, 30-minute snooze and 5-second display time. The custom sample uses 45 minutes, 10 minutes and 8 seconds, respectively. Values are synthetic and the countdown is static.

The summary labels today's period and includes an overnight example with Yesterday → Today, so an interval crossing midnight is understandable. Production period calculations must continue using the existing UTC boundaries, with local times and date-aware labels for display.

## Native preview scope

Fixture: `tools/DesktopRegressionChecks/PeriodicReviewPromptPreview.cs`; mode: `--periodic-review-prompt-preview`. Run only through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` on a private desktop. These are native Avalonia windows rendered with the actual shared frame; no browser or generated mockup images.

Captures: standard at 70%, 90% and 125%; custom timing and an overnight period at 90%. All five private-desktop captures passed native dimensions, text containment, centered shortcut badges, two actions and footer separation checks. Each capture was visually inspected. The native fixture build passed with zero warnings and errors.

Evidence and capture hashes are saved locally under `.superpowers/sdd/2026-11-18-periodic-review-prompt-preview/`. The only existing file changed for this spike is the regression runner's preview-mode registration.

This spike has no action dispatch, live countdown, scheduler, ActivityWatch request, history write or settings access. Production interaction and accessibility verification belong to implementation after design approval. No installation or Git publication is part of this design task.
