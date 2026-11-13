# Settings & Overlay Inspector — Appearance proposal

Design-only native preview for the next Settings layer. The approved Settings shell, frame, navigation and fixed footer are reused unchanged. This proposal is not integrated into production Settings and does not update the installed app or saved settings.

## Proposed organization

Use two sections inside the existing Appearance category:

- **App & panels:** application theme; Nika controller background, typography and sidebar artwork; page scale; generic application background/import/removal and background strength. Keep the existing catalogs and ranges, including the current 90% page-scale default. Nika controller styling is independent of global typography and stopwatch appearance.
- **Floating clock:** independent overlay theme, time format and timer text size. For Nika, show a direct route to the existing Stopwatch appearance editor and its frame/background/typography/artwork/hover settings. Keep automatic background-aware text colors. Other clock themes retain the existing text, font, outline and opacity fields; One Piece retains all nine transparency-part choices.

This is the proposed grouping, not a user-approved implementation decision. An uninterrupted form was considered, but it mixes independent application/clock choices and makes the current page very long. Moving these controls into more sidebar categories would change established navigation. Two internal sections preserve all thirteen categories and let the preview match the edited surface.

## Preview behavior

At 1120 × 720 DIP, form sections scroll on the left and a matching preview appears on the right. At 840 × 600, a compact preview and typography sample precede the form in one scrolling area. The outer Done/status footer remains fixed.

Application snapshots are rendered from the actual `ControllerWindow` using its existing synthetic-data constructor. Its timers are samples and no services are started. Typography samples use the controller's resolved fonts. Clock previews use `ThemedClockFace`. Every sample window is created on the private Windows desktop. Shared frame artwork is rendered at its existing scale, and controller snapshots are fitted uniformly.

The spike demonstrates local tab switching, Nika style selections, clock theme/format and text-size updates. Theme selection, page scale, generic backgrounds, imports/removals, other legacy controls and the link to Stopwatch appearance are layout proposals, not fully functional settings callbacks in this spike. No new runtime artwork or dependencies are introduced.

## Before production implementation

Inspection found `UiScalePercent`, `PanelBackgroundId` and `PanelBackgroundStrength` are read/written in Desktop Settings but lack a corresponding desktop application consumer. The page-scale and generic background paths must be connected and tested during implementation; the preview is not evidence that those controls work in the installed app. Preserve scope between controller-specific Nika choices, global fonts/colors and floating-clock appearance. Preserve immediate application, persistence failures, theme restoration, keyboard navigation and import validation.

## Evidence

- `application.png`: primary App & panels view with the actual Nika controller preview.
- `application-more.png`: page scale, generic background and disabled/default-state controls.
- `alternate-style.png`: Sky Island Chart, Chartmaker's Script and Cloud Dragon sample.
- `floating-clock.png`: Nika clock theme and route to detailed Stopwatch appearance.
- `classic-clock.png`, `classic-clock-more.png`: independent Midnight clock with legacy styling controls.
- `compact.png`, `compact-more.png`: compact layout and its lower form controls.

Run the native fixture with `--settings-appearance-preview` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. Minimal preview checks cover horizontal bounds, retained navigation, local section switching, scrolling and the fixed footer. This design pass does not run the full regression suite or validate live settings behavior.
