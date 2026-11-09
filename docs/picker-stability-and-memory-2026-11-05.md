# Standalone picker and artwork memory repair

The Navigator clock picker uses its own frame with transparent outer corners, without Windows caption/border chrome or a rectangular window background. Dragging its heading moves it; its native list, confirmation/close buttons, keyboard navigation and minimum size remain available. Other themes retain their existing picker.

The reported terminating crash was `InvalidOperationException: Cannot re-show a closed window`, reached through `ToggleActiveTimerOverlay` and `ApplyTimerOverlayVisibility`. The controller retained closed herd/separated windows in its caches. Closed events now evict those terminal windows and mark native-closed overlays hidden, so timer ticks keep them hidden and an explicit visibility action shows a fresh overlay for the same timer. Merge/delete cleanup is distinguished from native closure. Teardown snapshots separated windows because their Closed handlers remove dictionary entries.

`StopwatchAppearanceAssets` previously decoded every source image when any asset was requested. It now retains lightweight manifest definitions and loads only requested images, preserving shared bitmap and asset identity. Visited images remain cached; browsing every appearance can still load the whole catalog.

| Fresh-process first frame request | Before | After |
| --- | ---: | ---: |
| Decoded images | 40 | 1 |
| Decoded RGBA memory | 239.9 MiB | 6.0 MiB |
| First request elapsed | 7,609.5 ms | 247.6 ms |
| Process private bytes gained during request | 242.2 MiB | 5.8 MiB |

These are single cold-in-process measurements on a private Windows desktop, after Avalonia setup and before other controls. They measure artwork initialization, not steady-state CPU/GPU performance or every source of system lag. File-system caches, startup work and scheduling can affect elapsed time. Raw measurements: [baseline](performance/picker-stability-2026-11-05/baseline-artwork.json) and [final](performance/picker-stability-2026-11-05/final-artwork.json).

Verification reproduced all three failures before production edits. The focused native checks pass afterward: repeated native close/show for herd and separated overlays, unchanged timer identity/time, merge and two-window shutdown; actual HWND caption/border flags and transparent composition; root capture alpha; and demand-driven image loading. Existing Navigator/render/asset/shortcut regression passes 27 groups and 16 timer shortcut subcases, including all artwork choices. All native work ran on private desktops without switching or injecting input into the user's desktop.

The full unit suite passed 1,052/1,052 with zero skips after the final fix. Debug builds passed without warnings/errors. Independent review found that evicted windows could immediately reopen on the next timer tick; a strengthened regression reproduced that behavior before the final visibility-intent fix and passed afterward, together with all 27 existing native groups and 16 shortcut subcases. OS-level dragging/edge resize and physical-monitor DPI transitions were not automated; native transparency and programmatic layout sizes were checked.

Release single-file packaging succeeded. The stopped app's saved executable at `dist/desktop-windows/StopwatchOverlay.Desktop.exe` was backed up and replaced at 2026-11-05 21:53:27 +03:30. Installed SHA-256 matches the candidate: `6BAADC5FB4D5A992AA0ABD1442315A7E53D1DFCF436C80C63DFE4FB8579A90B8`. Previous SHA-256: `A4693EEA618C78F05C97898375DB49C44F99ECE9963B3CFCA4A27E266A76295D`; a matching backup is retained under this task's ignored `installed-backup-20261105-215316` directory. No user data was changed by installation. The app remains closed to avoid opening anything on the active desktop; the installed package has not been live-tested against the user's workspace.

Reproduce: build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then use `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` with `--picker-stability-only`, `--navigator-picker-regression`, or `--unit-tests` and a repository-local log path. Test logs are under `.superpowers/sdd/2026-11-05-picker-stability-and-memory/`.
