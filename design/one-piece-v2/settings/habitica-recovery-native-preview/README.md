# Habitica: Sync & Recovery design proposal

Status: native design proposal approved for implementation by the user on 2026-11-15. Implemented results are in `../habitica-recovery-implemented`.

## Structure

Recommend two tabs within the existing Habitica Settings page: **Connection & status** and **Sync & recovery**. A single expanded page would make connection fields and recovery actions compete for a long scroll. Keeping nested expanders would retain the current discovery problem. Tabs give these two jobs a clear home without adding a Settings sidebar entry.

The recovery tab has one full-width **Sync existing items** card and two recovery cards: **Restore missing tasks** and **Use a copied vault here**. It reuses the native Settings shell, fonts, colors, controls and shared frame. The clock overlay and border assets are unchanged.

## Revisions after the closer review

- The normal state shows no missing tasks and disables its review button. Two missing tasks appear only in the sample attention state.
- Each recovery confirmation opens inside its own card. Opening it moves keyboard focus to Cancel; cancel returns focus to the original action.
- Bulk progress counts confirmed changes as synced, separately from waiting or blocked items. The proposal includes ready, confirmation, running, paused, attention, completed, empty and interrupted examples.
- An interrupted queue preserves already queued changes and offers review again. Paused sync links back to Connection & status. An empty vault cannot start a batch.
- Vault transfer means transferring sync ownership, not copying files. Separate Windows and Ubuntu vaults can upload independently. A copy of the same linked vault still requires stopping sync on the old copy first.
- Recovery cards stack in narrower windows or with enlarged text. The main Done button remains fixed outside the scrolling content.

## Source semantics to preserve during implementation

`HabiticaIntegration.CountExisting` counts eligible local todos and reminders, including already linked items; the count is not a count of new tasks. Existing links update without matching remote tasks by title or importing remote tasks. `HabiticaImportProgress.Completed` counts acknowledged revisions. `CountMissing` covers linked targets reported NotFound, and recreation requires explicit confirmation plus enabled local ownership.

The production implementation should reuse the existing import, recreation and transfer handlers, including their account/vault guards and error handling. Sample counts and status text must be replaced with current integration state. Connection drafts and token masking must survive tab navigation. Final restore/transfer buttons are deliberately not connected to integration actions in this spike.

## Native previews and checks

Images are actual Avalonia renders with synthetic data. `ready.png`, `progress.png`, `restore.png` and `transfer.png` show the main proposal. Other captures cover confirmation, paused, blocked, completed, empty and interrupted states, plus compact layouts and enlarged text.

The fixture checks 70%, 90% and 125% page scale at 1120×720 and 840×600, plus 175% text at compact 125%. It checks horizontal overflow, recovery-card separation, keyboard focus, disabled states, tab navigation, confirmation cancellation and the fixed footer. The enlarged-text stress case intentionally has a small scrolling viewport because the existing shared header grows too; it is not a recommended everyday scale combination.

All checks run on a private Windows desktop. The fixture asserts zero HTTP requests, zero secret reads/writes and zero Settings saves. These checks validate the proposal, not live Habitica operations. The installed app is unchanged.

Reproduce after building `DesktopRegressionChecks` with the existing shared desktop runtime:

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-habitica-recovery-preview -LogPath .superpowers/sdd/2026-11-15-settings-habitica/recovery-preview.log
```
