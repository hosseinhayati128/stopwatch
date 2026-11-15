# Habitica Connection & Status implementation

The approved Connection, Connection status and Sync on this computer cards now use the original Habitica controls and integration handlers in Settings. The shared shell and frame are unchanged; other themes restore the legacy control tree.

- Save and Test apply the User ID, public client identifier and API-token drafts. Test saves first, as its helper explains. Enabling sync retains its existing save-first behavior. Blank token drafts retain existing credentials; saving a replacement clears and masks the editor.
- Show reveals only the entered draft. Rendering the page never reads a saved secret. Storage feedback belongs to the displayed account and distinguishes protected storage, session-only storage and a missing token. A successful test can verify token availability without claiming that its storage protection was verified; cached credentials never inherit another account's storage label.
- Test results, validation errors and request delays appear separately from local sync state. Busy actions prevent duplicate tests. Account edits, page navigation, theme changes and close mask the draft and reject stale test results.
- Invalid drafts show the existing validation explanation. They do not prevent pausing already-enabled sync. Canceled tests return to a ready-to-test state without replacing newer draft feedback.
- The sync toggle keeps its existing local-vault ownership behavior. Pending changes and the last successful sync use the existing integration status. Original bulk-sync and recovery controls remain under the collapsed Sync & recovery section with their confirmations intact.
- Cards stack at narrow widths or enlarged text. Editors grow vertically, actions remain keyboard-focusable, and Done stays outside the page scroll. The existing status timer stops when Habitica is hidden. No new dependencies, services or artwork were added.

## Native captures

These are actual production Avalonia controls rendered on a private Windows desktop. The fixture uses sample credentials, an isolated vault/state store, fake clipboard data, an in-memory secret store and a fake HTTP transport through the real integration. It does not contact a real Habitica account.

- `configured.png`: connection saved, sync paused and no previous sync.
- `connected.png`: successful simulated connection followed by enabling local sync.
- `connection-error.png`: rejected credentials with the saved token retained.
- `session-only.png`: storage fallback clearly requires re-entry after restart.
- `waiting.png`: retry delay with Test temporarily disabled.
- `compact-connection.png` / `compact-status.png`: stacked cards at 840 × 600.
- `large-fields.png`: 125% page scale and 175% typography.

Regular captures use 90% page scale at 1120 × 720 or 840 × 600. All fixture windows and dialogs remain on a private desktop without switching desktops.

## Verification

The final build completed with zero warnings/errors. Eight Habitica native groups, five Application/shell/Behavior groups, four Telegram groups, three Obsidian groups, six archived Habitica preview cases and all 1,053 unit tests passed (zero failed or skipped). All tests ran on private desktops.

The independent review identified storage attribution across accounts, canceled-test feedback, pausing with invalid drafts and missing validation explanations. New checks reproduced those findings before the fixes; the full verification set passed afterward. No deferred review findings remain.

One intermediate Telegram scan check encountered an access-denied error replacing its isolated outbox file. Its failure log was retained; the final rerun passed. Telegram implementation and fixture files were not changed. All 34 protected carryover-file hashes match the pre-task baseline.

Native checks cover explicit-save callbacks, token masking/reveal, blank-token retention, injected Paste, invalid-ID explanations, pausing with invalid drafts, theme round trips, keyboard focus, local sync ownership, successful and rejected responses, duplicate requests, stale/closed/canceled results, credential-write failure, multi-account and session-only storage, missing tokens, request delays and retained recovery confirmations. Layout checks cover both sizes at 70%, 90% and 125%, plus enlarged typography and complete feedback without overlap after collapsing recovery.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-habitica-native -LogPath .superpowers/sdd/2026-11-15-settings-habitica/native.log
```

Build, native, adjacent-page, unit-test and review results are recorded in `.superpowers/sdd/2026-11-15-settings-habitica/`. This task does not update the installed app or publish to Git. Live Habitica connectivity is not tested by these isolated fixtures.
