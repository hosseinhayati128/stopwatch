# Telegram Settings implementation

The approved Connection, Topics and Delivery cards now use the original Settings controls and real Telegram handlers. The layout reuses the Settings shell, frame and existing styles; other themes restore the legacy control tree.

- Preferences still apply immediately through the existing settings callback. The bot token starts masked, supports Show/Hide, and is masked again on configuration changes, navigation, theme restoration and close.
- Test connection verifies the bot and, if a chat ID is provided, sends a test message to the Quick notes destination. Its helper explains that action. A completed test is labeled as a past test, not a live connectivity guarantee.
- Delivery shows the actual queue count. Failed sends retain queued entries and visible error feedback. Busy actions cannot duplicate requests, and results from a changed configuration or closed window do not replace current feedback.
- Scan vault retains the three-day lookback, review/cancel dialog and sent receipts. It checks that the confirmation still belongs to the same vault and Telegram configuration before queuing or sending.
- Editors grow with text. At narrower effective widths the cards stack above the fixed Done footer. No new frame artwork, background polling or dependencies were added.

## Native captures

These are actual production Avalonia controls, rendered on a private Windows desktop using sample credentials, an isolated local vault/outbox and an in-memory HTTP handler. They do not show or contact a real Telegram account.

- `configured.png`: masked token, configured topics, ready-to-test and empty queue.
- `connected.png`: successful simulated response through the real connection handler.
- `pending.png`: failed delivery through the real service, retained queue items and redacted error that survives a newly queued item.
- `compact-top.png` / `compact-delivery.png`: stacked layout at 840 × 600.
- `large-fields.png`: 125% page scale and 175% typography.

Regular captures use 90% page scale at 1120 × 720 or 840 × 600. All fixture dialogs remain on the private desktop, without switching desktops.

## Verification

Both builds completed with zero warnings/errors. The independent review found a queue-growth feedback reset; a new failing native check reproduced it, the fix passed, and all validation below was rerun afterward. Private runs passed all four Telegram groups, five Application/shell/Behavior groups, three Obsidian groups, six archived preview cases, and all 1,053 unit tests (zero failed or skipped).

The native fixture covers preference callbacks, token masking/reveal, theme round trips, keyboard focus, blank credentials, connection routing, failed sends/retries and persistent feedback as the queue grows, duplicate clicks, stale results, close during a request, and vault-scan confirmation/cancellation. Layout checks cover 70%, 90% and 125% at both window sizes, plus enlarged typography.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-telegram-native -LogPath .superpowers/sdd/2026-11-14-settings-telegram/native.log
```

This implementation does not update the installed app or publish to Git. Live Telegram connectivity is not tested by the isolated fixtures.
