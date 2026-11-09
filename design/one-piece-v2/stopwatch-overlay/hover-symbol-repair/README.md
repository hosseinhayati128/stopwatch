# Hover symbols — 2026-11-05

Actual native toolbar renders, all twelve retained styles in running and paused states. `before.png` captures the original symbols; `after.png` is the revised native-size board; `after-2x.png` provides a larger export for inspection.

Close uses a balanced cross, play/pause share consistent proportions, and reset has a recognizable counterclockwise arrow. Small Vine Filigree and Royal Cartouche keys use a 16 DIP footprint; other vector sets use 20 DIP. Pixel symbols use an 8-by-8 grid of 2 DIP blocks, filled as one cached path to avoid seams between adjacent antialiased blocks. Play/reset paths and pixel shapes are cached. No runtime images, fonts, refresh timers or dependencies were added.

Verification: Debug build succeeded with zero warnings/errors. Ten native groups passed on a private desktop, including five shortcut-hook checks, all-style captures at 1x/2x, live toolbar action dispatch, disabled clock mode, theme restoration, opacity/cache behavior and inspector/preview isolation. The inspector reported native render scaling of 150%. Normal and compact glyphs were visually inspected. This visual repair did not change timer/action logic; the unrelated full unit suite was not repeated.

Reproduce after building the desktop regression harness: `Run-HiddenDesktop.ps1 -Mode '--toolbar-glyphs'` with a repository-local log path. Logs and Release packaging results are in `.superpowers/sdd/2026-11-05-hover-symbols/`.

Release packaging succeeded. The updated executable is `dist/desktop-windows-hover-fix/StopwatchOverlay.Desktop.exe`. The existing application was running (PID 4416), so its executable and process were left in place; exit that instance before launching the new build. The update has not been applied to the running process. Hashes are recorded in the task's ignored `build-status.json`.
