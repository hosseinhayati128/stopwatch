# Settings — Fonts & colors

Production implementation of the approved native Settings design. Reuses the existing editors, six typography scopes, settings persistence, theme styles, and modular frame; no new runtime artwork, dependency, or settings migration.

- Global text size (75–175%), theme colors, custom hex entry, and native color picker.
- Separate panel overrides with retained choices when returning to global text.
- Live sample rendered through the same typography service as application text.
- Original theme colors restored by Theme default; button, selected-item, and validation colors remain readable.
- Wide two-column and compact stacked layouts. At large text sizes in short windows, the page heading and tabs scroll with the controls; Settings navigation and Done remain fixed.

The Desktop consumer preserves bindings and base sizes, follows popup ownership, and updates painted clocks and cached Application previews. Slider gestures update the sample immediately and defer application-wide updates until completion. Changes use settings/control lifecycle events, without polling.

## Native evidence

All images are captures of production controls with synthetic settings on a private Windows desktop; the active desktop and installed application were not used.

- `wide-90.png`: global settings at the default page scale.
- `panel-override.png`: Dashboard & records with 115% text and theme accent.
- `invalid-color.png`: validation retains the last accepted color.
- `wide-{70,90,125}.png` and `compact-{70,90,125}.png`: page-scale matrix.
- Matching `-large-text.png` captures: the same matrix with 175% text.
- `compact-125-large-text-controls.png`: scrolling reaches the controls in the most constrained case.
- `text-{75,175,100}.png`: changing and restoring text size.

Run `--settings-typography-native` and `--settings-typography-regressions` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The focused checks cover scope inheritance, binding restoration, dynamic controls, inline text, popup ownership, painted clocks, selection colors, custom-color reopening, handled slider gestures, deferred work, preview invalidation, and lifecycle cleanup. Regressions cover Settings shell/actions, Appearance actions/previews, 30 controller sizing states, overlay resource caching, and Dashboard layout with the typography service enabled.

The native harness does not install the build or exercise live user data. A release was subsequently installed and launched at the user's request, with matching file hashes, all 12 timer IDs loaded, and no new crash logs during the startup check. The previous build was retained locally for rollback.

Verified 2026-11-13: Desktop and harness builds succeeded with zero warnings/errors; all 9 focused native groups and 7 regression groups passed. Before publication, the full unit suite also passed (1,052 tests), and the archived design-preview command was checked against the integrated layout. The unit-project build reported NU1900 warnings because NuGet vulnerability metadata was unavailable; compilation and tests completed successfully.
