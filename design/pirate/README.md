# one piece application theme

## Notes and typography

The add note/todo/reminder popup and notes viewer now use the reference's walnut frame, brass corner guards, parchment surface, and compass-ended action buttons. Text remains editable and the viewer retains its filters, completed-todo styling, scrollable list, and auto-close behavior. Painted components are stretched with preserved corners. Asset paths and the built-in imagegen prompts are recorded in [notes-asset-prompts.md](notes-asset-prompts.md).

**Settings → Fonts & colors** controls global text size (75–175%) and color. Choose Theme default to retain each component's original colors, a theme text/accent/muted preset, a supplied swatch, a custom hex color, or the Choose color picker. The **Customize individual panels** section starts collapsed. Check Controller, Dashboard & records, Settings, Notes/todos/reminders, Shortcuts & dialogs, or Floating overlay to edit that section separately. Unchecking restores global inheritance while retaining the saved override for later reuse. Page scale remains a separate setting for resizing whole layouts.

Run `dotnet run --project tools/PiratePreview -- --notes-only` for production-XAML note previews or `--typography-only` for font-setting interaction checks and previews. These use synthetic data and do not open or write user settings, history, or vaults.

Select **one piece** under **Settings → Appearance → Application theme** in the new build. Saved selections under its former name, Pirate, migrate automatically.

The controller, project dashboard, records, settings, shortcuts, project chooser, note windows and confirmation dialogs share the pirate artwork and typography. A floating clock set to Follow Application Theme resolves to one piece; one piece is also an independent floating clock choice. The navigator overlay recreates the supplied wooden chart, rope, compass, parchment map, riveted brass plate and blue chart beneath three compass action dials. Default timer fonts use live seven-segment digits; explicit custom fonts remain supported. Existing timer sessions, history, shortcuts and editable data are preserved.

**Settings → Appearance → Timer surface opacity → Transparency details** starts collapsed. Checked parts stay opaque; unchecked parts follow the opacity slider. Nine persisted choices separately control the clock frame/rope, parchment, metal border, metal fill, digits, project name, button frame/chain, blue map, and brass dials/icons. By default the clock frame, metal rim, live text and compass buttons stay visible while the backgrounds fade. Both the real overlay and Settings preview use the same rendering. Choices survive theme switching and old settings receive these defaults automatically.

**Settings → Appearance → Page scale** resizes page text, controls and artwork from 70% to 125% in 5% steps. The default is 90%. Mouse and keyboard adjustments apply when the interaction finishes, and the preference survives restarting. Floating timer text retains its separate Text size control. Narrow settings windows prioritize navigation and the inspector over the preview; dialogs can scroll, and the controller sidebar responds to the scaled space.

## Visual implementation

- Dark walnut frame and navigation, four brass corner guards, faded straw-hat skull in the timer rail.
- Torn parchment, coastlines, two compass roses, treasure mark, and sea dragon.
- Live, outlined Almendra numerals, responsive title, all four timer modes, native editable countdown inputs.
- Riveted brass controls with textured patina, lion and ram figures, telescope, anchor, swords, paintbrush, hammer, and Log Pose.
- Mouse hover, pressed, disabled, and keyboard-focus states.
- Scrollable compact controller; the status strip stays visible. The rail follows the existing 820-DIP breakpoint.
- The theme uses an initial 1200 × 920 window; user resizing remains available.
- Shared parchment cards, brass/teal sliders and switches, dropdown arrows, circular red activity heatmap and glass-colored chart gauges.
- Application and floating clock palettes retain their typed resource contracts, with independent floating-clock font/color/opacity overrides.

The references are flattened raster mockups, without original font or layered artwork files. This implementation is a close visual recreation, not a pixel-identical extraction. Almendra and Barlow Condensed are bundled and do not require font installation. Native Windows title-bar controls are retained.

## Assets and provenance

The built-in image generation tool was used with the user-supplied controller JPEG as the reference. Final production assets live in `StopwatchOverlay/Assets/Pirate/`:

- `controller-background.png` — cleaned wood/parchment decorative plate.
- `icons.png` — transparent pirate icon atlas.
- `brass-plate.png` — weathered brass texture.
- `navigation-tools.png` — Log Pose and hammer.
- `navigator-map.png` — wooden frame, rope, compass and parchment chart.
- `navigator-plate.png` — scalloped metal timer plate and rivets.
- `navigator-toolbar.png` — wooden control frame, chain and blue map.
- `navigator-dial.png` — brass compass button body.

[Controller asset prompts](asset-prompts.md) and [navigator asset prompts](navigator-asset-prompts.md) record the built-in image generation specifications. The navigator assets use `Floating_clock_UI_design_20260921102153.jpeg` as their reference. WPF clips the artwork into independently adjustable layers; time values, project names and action glyphs are live.

Fonts are bundled in `StopwatchOverlay/Assets/Fonts/Pirate/`. Source families:
[Almendra](https://github.com/google/fonts/tree/main/ofl/almendra) and
[Barlow Condensed](https://github.com/google/fonts/tree/main/ofl/barlowcondensed).
Their SIL Open Font Licenses are included and copied beside published builds.

## Validation and preview

```powershell
dotnet build StopwatchOverlay/StopwatchOverlay.csproj
dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj --no-restore
dotnet run --project tools/PiratePreview/PiratePreview.csproj
```

The renderer loads the actual controller XAML and constructs other production pages with fictional in-memory data. It does not construct the production controller, open user settings/history stores, register hotkeys, or show native windows. Captures cover controller sizes and modes, Settings at 70/90/125%, minimum windows, dashboard and dialogs, and switching back to Midnight. Routed Settings slider checks verify deferred application, transform replacement and isolation from floating timer size. Native global-hotkey behavior is outside this visual harness.

Screenshots in `previews/` are real WPF renders with synthetic data, not image-generation mockups.

Run the preview command with `-- . --overlay-only` to render the navigator at 0%, 50% and 100% opacity and its collapsed/expanded Settings controls. This also checks all nine checkbox interactions and independent overlay-theme visibility. Pixel regression tests verify retained borders/dials, individual layer selection, custom fonts, long times, scale extremes and theme switching. Settings tests cover all 512 layer combinations on save/reload.
