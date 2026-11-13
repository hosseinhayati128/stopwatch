# Fonts & colors — native design proposal

Approved design snapshot. Production implementation and its native captures are documented in [typography-implemented](../typography-implemented/README.md). These original proposal images remain available for comparison.

The existing Settings shell and modular frame surround two sections:

- **Global text:** the existing 75–175% size slider, theme/preset colors, custom hex field and native color picker. A summary links to panel exceptions.
- **Panel overrides:** choose one of the existing six scopes, enable its separate settings, and edit the same controls. Disabling an override preserves its choices and restores inheritance.

A live native text specimen shows a heading, project label, timer digits, secondary text, selection, and accent link. The proposal keeps selection-state text readable when the ordinary text color changes. Text size preserves relative proportions; page scale and font-family themes remain in Appearance.

Wide content uses two columns; narrow content stacks the editor and specimen inside a scroll area. Navigation and Done remain outside that scroll area. Window sizes are 1120 × 720 and 840 × 600; each is captured using the actual page-scale service at 70%, 90%, and 125%. Layout adapts to available logical width, so a small window at 70% can still fit two columns.

## Review captures

- `wide-90.png`: global settings at the default page scale.
- `panel-override.png`: Dashboard & records, 115% text, theme accent.
- `panel-inherited.png`: the same panel follows global settings while retaining its choices.
- `custom-color.png` / `invalid-color.png`: native hex entry and validation.
- `wide-{70,90,125}.png` / `compact-{70,90,125}.png`: scale matrix. `-more` captures show the bottom of stacked content.

## Implementation boundary

This fixture reuses the actual SettingsWindow, TypographyEditor controls, theme styles, scale service, fonts, and frame renderer. Its live specimen reacts to the existing editor events using synthetic in-memory settings; it does not demonstrate application-wide typography integration.

At proposal time, Desktop had typography editor/persistence hooks but no general typography-application service equivalent to the older WPF `TypographyManager`. The subsequent production implementation adds that integration, scope routing, binding preservation, theme restoration, popup text, special timer rendering, and protected selection-state colors. The preview fixture itself remains a design specimen.

## Verification

Run `--settings-typography-preview` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. The fixture uses a private Windows desktop without switching the active desktop, never starts the real application, and attaches no persistence subscriber.

`verification.json` records the six native window/scale cases. Checks cover page scaling, horizontal bounds, fixed Done position, all 13 navigation items, all six panel choices, override inheritance, and valid/invalid hex input. These are preview checks, not a full application regression run.
