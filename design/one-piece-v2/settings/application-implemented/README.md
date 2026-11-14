# Application page implementation

Native captures show the integrated Settings Application page with sample preferences. Startup uses the existing Start with Windows control. The close choices update the existing close-action preference and save callback. Alternate themes retain their Application controls.

- `ask.png` shows Ask every time with startup off.
- `tray.png` shows Minimize to tray with startup on.
- `exit.png` shows Exit Stopwatch with startup off.
- `compact-tray.png` shows the scrolled compact layout with its fixed Done footer.
- `large-text.png` shows the compact page at 125% scale and 175% text size, scrolled to the exit choice.

The build completed with zero warnings and errors. Five private native check groups passed. Layout checks covered all three close choices at 1120 × 720 and 840 × 600, at 70%, 90% and 125% scale. Preference checks covered loaded values, single callbacks, external reload, keyboard access and theme restoration.

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-application-native -LogPath .superpowers/sdd/2026-11-14-settings-application/native.log
```

Windows sign-in startup and actual process shutdown were outside these UI checks. Installing the new build is a separate step.
