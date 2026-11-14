# Obsidian / Markdown native design preview

The proposal uses the current Settings & Overlay Inspector shell, shared panel frame, fonts and colors. Export preferences and Destination & export sit side by side when there is space and stack in a narrower view. The page scrolls above the fixed Done footer.

Export preferences retains the automatic-export and unnamed-timer choices. Destination & export contains the target folder with Browse, filename, full output path, Export all history, and feedback beneath the action. The existing path preview supplies the default filename and adds the Markdown extension. Missing destinations have a disabled export action. Success and failure use text as well as color.

## Preview files

- `configured.png` — both preferences enabled and a sample destination selected.
- `exported.png` — sample successful export feedback.
- `export-error.png` — sample folder permission failure with recovery guidance.
- `unconfigured.png` — no target folder, preferences off and export disabled.
- `compact-top.png` — stacked cards at the top of the compact page.
- `compact-destination.png` — compact page scrolled to a long destination and sample success feedback.

## Scope and validation

Only the native preview fixture, its command dispatch, and these design artifacts were added. The fixture reuses the existing named preference and path controls with an in-memory sample settings object. Browse and Export are separate prototype buttons with sample behavior. No real export, folder picker, history provider or persistence subscriber is used. The running app, user vault and clock-overlay artwork are unchanged.

The fixture build passed with zero warnings and errors. All six private-desktop layout cases passed at 1120 × 720 and 840 × 600, each at 70%, 90% and 125% page scale. Checks cover sample preference updates, default and long paths, automatic `.md` suffix, horizontal fit, export focus and the fixed footer. Images use actual Avalonia rendering at 90%.

This is a design proposal. Production integration, real folder picking, export results and full export-behavior regression checks remain for implementation after design review. Feedback counts and timestamps in the images are sample data.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true -p:BuildProjectReferences=false
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-obsidian-preview -LogPath .superpowers/sdd/2026-11-14-settings-obsidian-preview/native.log
```
