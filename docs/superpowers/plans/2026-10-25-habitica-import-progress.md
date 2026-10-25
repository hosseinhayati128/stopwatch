# Habitica existing-item sync progress

**Goal:** Make confirmation visible and show durable completed/total counts, request waits and blocked items after Sync existing items.
**Architecture:** Track an account/vault-scoped import batch against intended target acknowledgements in optional metadata. Expose progress and next request time from GetStatus. Queue asynchronously and keep confirmation/progress immediately beside its action.
**Constraints:** Preserve Habitica request spacing and existing ownership/account safeguards. Never use live task mutations in tests. All GUI tests use the private virtual display. Keep existing themes.

- [x] Core regression: batch progress, restart, failures, isolation and repeated enrollment.
- [x] Core implementation: durable batch and honest acknowledgement counts.
- [x] UI regression: confirmation placement, preparing, 2/43, waiting, paused, blocked and done states.
- [x] UI implementation: asynchronous queue, visible confirmation, progress bar and readable status.
- [x] Run core and hidden native UI checks, inspect all themes, publish/install/restart. Do not push unless asked.

Verification: fake-service recovery/multi-computer checks and hidden native Habitica UI checks passed, including visible viewport, global token error, blocked countdown, acknowledgement counts and stale import guard. Three themes inspected; Linux self-contained publish and independent application launch verified. No live tasks were created by tests. This PC lacks secret-tool, so the existing session-only credential must be re-entered after restart.
