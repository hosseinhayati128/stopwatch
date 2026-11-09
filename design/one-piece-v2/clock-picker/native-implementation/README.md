# Native Navigator clock picker

Implemented in the local build on 2026-11-05. These are captures of the actual Nika picker controls, replacing the earlier static row study. The later standalone-window and memory repair was installed into the saved app build with a verified backup; the app remains closed. See `docs/picker-stability-and-memory-2026-11-05.md`. No Git publication was performed.

The picker uses native ListBox items, buttons, text, tooltips and automation peers inside the shared Engraved Filigree surface. Per-open snapshots retain the original timer order/identities and time sources. No refresh loop, runtime artwork, font, dependency or preference was added. Other application themes keep the original picker layout.

- `navigator-native.png`: actual row states, names/modes and a 12-clock list.
- `row-states.png`: current Illustration, hovered Coding and selected Reading remain independent.
- `names-and-modes.png`: long name, 123-hour time, Persian name, live clock, countdown, timecode and fallback timer name.
- `twelve-clocks.png`: entries through 12, with ACTIVE and selection together. Positions 10+ use plain numbers; digits/numpad 1–9 retain absolute positions after scrolling.
- `minimum-size.png`: 360 × 300 with complete time, header/close and confirmation controls inside the frame; remaining rows scroll.
- `names-and-modes-1.5x.png` / `names-and-modes-2.0x.png`: full native control subtree at explicit export scales. These are not physical-monitor-DPI or live screen-reader verification.
- `standalone-picker.png`: native Window root with transparent exterior. A separate private-desktop check verifies actual transparent composition and the absence of native caption/border styles; subtree captures alone do not prove those native properties.

Names trim inside their own space; times remain complete, including the maximum TimeSpan elapsed value. Full names, time, status, mode, category and separated status are retained in tooltips and accessible list-item names. Clock mode has a live-clock glyph. Native single click selects, double click/Enter/Select clock confirms, Escape/close cancels, and arrows/End scroll through the list.

Verification passed on private Windows desktops: 27 native groups, 16 timer shortcut subcases, and 1,052 unit tests. The input checks call the private window's real Avalonia input callback and mouse device, including hit-testing, without OS input injection. They cover hover versus selection, click/double-click, button hover/press, close focus/press, maximum hours, recycling, automation peer names, minimum size, other themes and prior overlay opacity/performance guards.

Independent review found two button issues: default Fluent template-child paint/scaling overrode the intended chrome, and close base styling hid pressed/focus feedback. Both were reproduced by native regressions before correction and passed afterward. Visual inspection also found minimum-size child clipping in the shared frame; an explicit viewport regression reproduced it. The frame now measures its child using the real final interior before arranging. The higher-resolution export host is explicitly arranged at zero origin so monitor size constraints do not crop the exported subtree.

Reproduce after building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj` with `Run-HiddenDesktop.ps1 -Mode '--navigator-picker-regression'` and a repository-local log path. `--navigator-picker-only` runs the seven focused groups and regenerates these captures. Logs and unit TRX are under `.superpowers/sdd/2026-11-05-navigator-clock-picker/`.
