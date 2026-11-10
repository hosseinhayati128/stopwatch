# Running Clocks — native integration

The real `Win+F2` → `H` shelf now uses the approved earlier layout in Nika: 350 DIP wide, 44 DIP rows, up to five visible rows, and the shared Engraved Filigree frame. Header and Hide remain outside the scrolling area. Other application themes retain their plain shelf.

These are captures of the application controls, not drawn fixture buttons:

- [Three timers](shelf-three.png)
- [Longer list](shelf-many.png)
- [Empty](shelf-empty.png)

Selecting a name activates its timer without stopping others. Pause retains elapsed time and removes only that running row. The next remaining name receives keyboard focus and is scrolled into view after layout; when empty, focus moves to Hide. Hide leaves timers running. Names trim, Persian names use RTL text, and full controller-formatted times keep their own space.

The native check uses synthetic controller settings and temporary history storage. `--running-clocks-only` covers layout/filtering, actions/focus, managed pointer and keyboard input, long times/RTL/scrolling, retained controls, theme isolation/reopening, monitor-edge placement and captures. Run it with `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`; it never switches the active desktop.

No artwork, fonts, dependencies, settings or refresh timer were added.

Verification on 2026-11-09: all seven shelf groups and the shortcut regression passed on the private desktop; all 1,052 unit tests passed. Independent review found no actionable issues. Release was copied to `dist/desktop-windows` only after confirming the app was closed, with a hash-verified rollback in `dist/update-backups/running-clocks-20261109-214709`. Installed executable SHA256: `E44C135B60480581CBEE957A6C6E7BB360D1D43AF470F0604F14E807B646AEB1`. The app was left closed.

Limits: physical mixed-DPI monitor behavior and screen-reader announcements were not exercised. The unit build's NuGet vulnerability audit reported an unavailable feed (NU1900); compilation and tests succeeded. Receipts are under `.superpowers/sdd/2026-11-09-running-clocks-native/`.
