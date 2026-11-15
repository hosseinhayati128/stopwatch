# Current Habitica Sync & recovery examples

These captures explain the existing controls, not a proposed redesign. They use the production Habitica panel on a private Windows desktop at 90% scale, scrolled to its recovery section. The installed app and production code were not changed.

- `expanded-structure.png`: Sync existing items, the expanded Copied or shared vault section with Move this linked vault here, and Recreate missing linked tasks.
- `sync-confirmation.png`: the real confirmation for three sample local items: Read chapter 2, Practice drawing, and Review weekly plan.
- `queued-progress.png`: after confirming, the real local queue reports three pending items and 0/3 synced. No worker or live request is run, so these examples do not claim successful remote delivery.

Sync existing items enrolls existing local todos/reminders; already linked items are updated and existing Habitica tasks are not imported or title-matched. Recreate missing linked tasks is an explicit recovery action for previously linked tasks Habitica reported missing. Move this linked vault here transfers ownership of the same copied/shared linked vault; separate independent vaults can sync concurrently without that transfer.

Connection & Status was the completed design layer. The existing advanced controls were preserved inside a collapsed expander for access while their own design remains pending. The expander currently retains legacy styles.

Only the example fixture/entry point and these captures were added. The fixture uses an isolated sample vault, in-memory credentials and a fake transport. It asserts the three-item confirmation, the resulting local queue and zero HTTP requests. The build passed with zero warnings/errors; the private native example check passed.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true -p:BuildProjectReferences=false
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-habitica-recovery-examples -LogPath .superpowers/sdd/2026-11-15-settings-habitica/recovery-examples.log
```
