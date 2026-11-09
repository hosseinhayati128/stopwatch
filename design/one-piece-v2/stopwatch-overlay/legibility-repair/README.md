# Hover alignment and background palettes — 2026-11-06

The hover action centers now follow the measured inner faces of the shipped artwork, with individual x/y anchors in the asset manifest. Reset and play glyphs also account for their actual ink bounds. All 12 controls retain the existing buttons, actions and 34 DIP hit targets; no artwork was regenerated or stretched.

Each of the 12 stopwatch backgrounds supplies coordinated colors for time, project name, running/paused markers and recording. Nika no longer inherits the legacy fixed-white text setting. Other overlay themes keep their saved color, and Settings explains Nika's automatic palette. Aged Bronze, Tideglass and Sky Island add a thin contrasting edge to separate text from the texture. Glyph geometry uses a bounded cache; the project-name layout is retained between ticks.

- [All backgrounds on dark and light desktops, 70% background opacity](background-palettes.png)
- [All hover controls, running and paused, 2× native capture](centered-controls.png)

Validation uses `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` and a private Windows desktop without switching the user's desktop. The baseline reproduced both misplaced button centers and the white-text override. `--overlay-legibility` passes the measured-center regression, all 12 legacy-color comparisons, real overlay actions/opacity, stable tick layout, retained toolbar state and Settings preview isolation at 150% native scaling. All 10 typography choices were also captured on the three textured backgrounds. Build completed without warnings or errors. Live settings bytes were unchanged.

Raw captures, logs and the bounded `--stopwatch-performance-palettes` workload are in `.superpowers/sdd/2026-11-06-overlay-legibility/`. The redraw sample measures the fixed fixture only, not the user's entire workload.

The native redraw sample completed 180 controller updates per scenario (paused, seconds, tenths), after 45 warm-up updates. Sky Island's cached contrast edge used 0.19 / 0.23 / 0.52 CPU seconds across roughly 7.4–7.6 seconds per scenario (0.32 / 0.39 / 0.85% of this eight-logical-processor machine). This is a bounded smoke measurement, not a before/after application speed claim; a single fixture pass also includes process warm-up/order effects.
