# ActivityWatch Review Prompts implementation

Spec: `design/one-piece-v2/settings/activitywatch-review-native-preview/README.md`.

Goal: integrate the approved native Settings cards and make automatic review prompts work in the desktop app. Preserve the existing review editor, records, overlay and other Settings tabs.

Constraints: use the current checkout; no deployment, commit or push. Run all native windows and tests on the private desktop. Sample settings and isolated data only. Reuse existing styles, scheduler and prompt window; no dependencies.

## Task 1: scheduler lifecycle

Interfaces: PeriodicReviewService retains its public API for legacy WPF and gains optional TimeProvider for deterministic checks. Desktop consumes PromptRequested and outcome methods.

1. Add failing tests for timing, no duplicate prompts, disabled/disposed state and recent review periods. Build tests, run through Run-HiddenDesktop.ps1 --unit-tests. Expected: new behavioral tests fail before production changes.
2. Make scheduling thread safe, snapshot settings on configuration, schedule one timer per deadline, and cancel on disable/dispose. Keep existing interval/snooze semantics.
3. Repeat private unit checks. Expected: all pass.

## Task 2: Settings cards and desktop integration

Interfaces: existing named Settings controls/events and controller manual review; shared ActivityWatch tab/restore lifecycle; PeriodicReviewService and PeriodicReviewPromptWindow.

1. Add failing native checks for production cards, range validation, side-effect-free examples, configured shortcut, restoration, and controller prompt lifecycle. Expected: missing cards/lifecycle fail on private desktop.
2. Reparent existing controls into the approved responsive cards. Preserve original properties and Grid columns when restoring old themes. Validate ranges before persisting; invalid timing never blocks manual review, invalid activity detail does.
3. Start/stop the scheduler with the desktop controller; marshal requests to UI; suppress stale/duplicate prompts, defer while editing, and release the guard on review close. Keep manual review available with automatic prompting and ActivityWatch off.
4. Run private native checks and adjacent connection/export/archived preview checks. Expected: all pass, clean captures at 70/90/125%, compact and large text.

## Task 3: result and final review

1. Inspect actual native captures and fix any clipping or state problems. Write implementation evidence README.
2. Run relevant full unit/native suites after final changes. Expected: zero failures and clean builds.
3. One fresh whole-change reviewer (gpt-6-astra, high), read-only including untracked sources. Rule on findings; fix Critical/Important once with RED/GREEN and relevant suite; record minor/deferred items. No review loop.
4. Show native results in chat; report checks and any live deployment limitations. Do not update the installed app or publish Git changes.
