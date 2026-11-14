# Application native design preview

The Application page uses the current Settings & Overlay Inspector shell with two cards. Startup holds the Start with Windows checkbox. Close button shows the existing three actions as radio choices with their effects visible beneath each label. The selected choice uses the navy selection color from the other Settings pages.

The cards sit side by side in the wide layout and stack when space is limited. The page scrolls above the fixed Done footer. The proposal uses native Avalonia controls, the shared panel renderer, and sample settings.

- `ask.png` shows Ask every time with startup off.
- `tray.png` shows Minimize to tray with startup on.
- `exit.png` shows Exit Stopwatch with startup off.
- `compact-tray.png` shows the compact layout scrolled to the final close choice.

The preview build completed with zero warnings and errors. The private Windows desktop fixture checked the three close choices and startup toggle at 1120 × 720 and 840 × 600, each at 70%, 90%, and 125% page scale. Checks covered horizontal fit, the fixed footer, focus, and updates to the sample model.

This is a design proposal. Production integration and live startup and exit verification are pending.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-application-preview -LogPath .superpowers/sdd/2026-11-14-settings-application-preview/native.log
```
