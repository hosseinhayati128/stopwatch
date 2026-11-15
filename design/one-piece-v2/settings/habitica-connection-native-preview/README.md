# Habitica Connection & Status — native design proposal

This first layer uses the existing Settings & Overlay Inspector shell, shared frame, typography and parchment/navy palette. The left card contains connection fields and explicit Save/Test actions. The right side separates connection feedback from sync status on this computer. At narrow effective widths, the cards stack above the fixed Done footer.

The split layout keeps account setup and its result visible together on a wide window. A single long column would require more scrolling there; the proposal uses that arrangement only when space or text size requires it. This is a design spike for review, not production integration.

## Connection

- Retain User ID, the public client identifier, and the masked API-token draft. Keep the public identifier distinct from the secret token.
- Keep Paste and Show/Hide beside the token. Show reveals only the entered draft, is disabled when it is blank, and never retrieves a saved token.
- Save connection is explicit. Saving clears the token editor; a blank draft keeps the current token, whether it is protected by the OS or retained for this session.
- Test connection saves the entered details before checking the account, matching the existing handler. The helper states this behavior.
- Setup has its own watermark/help copy and no saved-token claim. Empty account details disable the sample Save/Test actions.

## Connection status

Show the outcome of the last test separately from credential storage. A successful past test is not an ongoing connectivity guarantee. Preserve distinct feedback for setup, saved details, unsaved edits, rejected credentials and a delayed retry. The storage summary distinguishes a protected token from a session-only token that must be entered again after restart.

## Sync on this computer

Retain the local sync toggle, pending-change count and last successful sync. Explain enabled, paused and attention-needed states with text as well as color. State that syncing continues while Stopwatch is open, including in the tray, and that todos, reminders and checklist items sync while standalone notes stay local.

Bulk syncing existing items, batch progress, recovery confirmations and copied-vault transfer controls belong to the next design layer. This preview does not remove those controls from the application.

## Captures

- `connected.png`: saved connection, successful past test and enabled sync.
- `setup.png`: no account or token saved yet.
- `token-draft.png`: masked sample token, enabled Show action and unsaved feedback.
- `paused.png`: sync paused with queued changes retained.
- `connection-error.png`: rejected account/token feedback and three pending changes.
- `session-only.png`: working connection with storage unavailable until restart.
- `waiting.png`: delayed retry with the existing connection kept.
- `compact-connection.png`: stacked layout at the top of the page.
- `compact-status.png`: stacked status cards scrolled into view.
- `large-fields.png`: 125% page scale with 175% typography.

Regular captures use 90% page scale at 1120 × 720 or 840 × 600. These are actual Avalonia renders, not generated artwork.

## Isolation and validation

The fixture supplies independent sample controls and sample-only button feedback. It never calls Habitica actions, reads the clipboard, or saves credentials. The shell receives empty production Habitica account/vault settings; the original Habitica panel is detached before showing the window. The Telegram outbox also points to a separate empty fixture path. There is no controller, settings-persistence subscriber or live integration worker.

Only the preview fixture, its Program mode and these design artifacts are added in this task. Existing uncommitted Telegram work remains separate. The installed application is not changed.

The preview build passed with zero warnings/errors. All six native cases passed.

Native checks cover six size/scale combinations (1120 × 720 and 840 × 600 at 70%, 90%, 125%), sample token masking/reveal and clearing after Save, empty-configuration actions, keyboard focus, horizontal fit and the fixed footer. The compact 125% case additionally checks editor line height and action-caption width at 175% typography. All windows remain on a private Windows desktop, without switching desktops.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true -p:BuildProjectReferences=false
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-habitica-preview -LogPath .superpowers/sdd/2026-11-14-settings-habitica-preview/final-native.log
```

Production behavior, live Habitica requests and credential storage are not tested by this design fixture. Implementation remains a separate step after design review.
