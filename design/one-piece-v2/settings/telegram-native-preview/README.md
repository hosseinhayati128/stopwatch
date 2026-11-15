# Telegram Settings native design preview

The proposal uses the existing Settings & Overlay Inspector shell, shared frame, fonts and colors. Connection sits on the left, with Topics and Delivery on the right. At narrower effective widths the three cards stack in that order and scroll above the fixed Done footer.

## Sections

- **Connection** retains the enable-sync preference, bot token and group/chat ID. The token is masked by default with a Show/Hide action. Test connection has visible explanatory text because the existing service verifies the bot and, when a chat ID is supplied, sends a test message to the Quick notes destination. A separate feedback box shows ready, successful and failed test states.
- **Topics** shows a topic-ID editor beside Quick notes, Todos and Reminders. Blank values use General / the main chat. The underlying field values remain the existing strings.
- **Delivery** shows queue feedback, Send pending and Scan vault. Empty queues disable sending. The scan helper describes the current three-day lookback and review-before-send flow. The pending example says items remain saved locally for retry. A previous successful connection test is labeled as a past test rather than an ongoing connectivity guarantee.

Editors grow with text size. The token-reveal button sizes to its caption with a 60px minimum, avoiding the clipped label found in the first enlarged-text capture.

## Captures

- `configured.png` — sample connection details, ready to test, empty delivery queue.
- `connected.png` — sample successful connection test.
- `pending.png` — a previous successful test and three items waiting after a delivery failure.
- `connection-error.png` — sample invalid-token feedback.
- `scan-result.png` — sample scan feedback. The existing confirmation dialog itself is not redesigned or invoked by this prototype.
- `unconfigured.png` — sync off, empty token/chat/topic fields, unavailable actions disabled.
- `compact-top.png` — the start of the stacked layout.
- `compact-delivery.png` — stacked Topics and Delivery, scrolled into view.
- `large-fields.png` — 125% page scale and 175% typography, showing readable editor contents and the complete Show label.

Regular captures use 90% page scale at 1120 × 720 or 840 × 600. These are actual Avalonia renders, not generated artwork.

## Isolation and checks

Only the native preview fixture, its Program dispatch and these design artifacts were added. The fixture uses an in-memory AppSettings instance, a deliberately nonfunctional sample token, a sample chat ID and a separate empty outbox path. There is no controller or persistence subscriber. New prototype buttons simulate connection, sending and scan feedback without invoking the real network, delivery or vault-scan handlers. No Telegram requests or messages are made by the preview actions, and no live credentials or delivery queue are read.

The harness built with zero warnings and errors. Six private-desktop cases passed at 1120 × 720 and 840 × 600, each at 70%, 90% and 125% page scale. Checks cover sample preference updates, token masking/reveal, configured and empty action states, horizontal fit, focus and the fixed footer. The compact 125% case additionally checks editor line height and button-caption width at 175% typography. All test windows remained on a private Windows desktop, without switching desktops.

This is a design proposal. Production integration, actual connection/delivery behavior and the existing vault-scan confirmation flow remain for the implementation step after review. No installed-app update or Git publication is part of this preview task.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true -p:BuildProjectReferences=false
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-telegram-preview -LogPath .superpowers/sdd/2026-11-14-settings-telegram-preview/native.log
```
