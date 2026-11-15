# Habitica Sync & Recovery: implemented native controls

The Habitica navigator page now has Connection & status and Sync & recovery tabs. Recovery contains bulk sync, explicit missing-task recreation and copied-vault ownership transfer. Confirmations appear inside the appropriate card, focus Cancel, and return focus on cancellation.

The existing guarded integration actions and acknowledgement-based progress are reused. Connection drafts stay masked and are not saved by tab navigation. Recovery actions require saved connection details; bulk sync and recreation also require enabled local ownership. Account/vault changes invalidate pending confirmations. Local inventory is read in the background on opening Recovery, with stale results discarded. No inventory scan runs on the two-second status timer.

Screenshots show actual production Avalonia controls in the shared Settings shell, using isolated sample data. `ready.png`, `confirmation.png`, `progress.png`, `paused.png`, `blocked.png`, `complete.png`, `interrupted.png`, `restore.png`, and `transfer.png` cover the main states. Compact captures and `large-text.png` cover resizing. Status-only examples use synthetic integration snapshots; action checks queue and recreate real sample metadata through the production handlers. No live Habitica requests are made.

Validation: build the native fixture, then run its `--settings-habitica-recovery-native` and `--settings-habitica-native` modes through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The new checks cover 70%, 90%, and 125% page scale in regular/compact windows, plus enlarged text, empty inventory, drafts, cancellation, confirmation scope, copied-vault transfer, recreation, theme restoration and hidden refresh loops.

The installed application and clock-overlay assets are unchanged by this implementation task.

Final verification on 2026-11-15: build passed with zero warnings/errors; all 3 recovery groups and 8 connection groups passed, as did the archived native preview and 1,053 unit tests (zero skipped). Incomplete batches cannot be reviewed again until completion or an explicit interruption/error. Legacy themes restore action availability correctly. All final test runs used private desktops and temporary fixture vaults.
