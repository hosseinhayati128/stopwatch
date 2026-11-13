# Settings Appearance — native implementation

The Appearance category now has **App & panels** and **Floating clock** sections inside the existing standalone Settings frame. The original settings controls, catalogs, immediate-change events, import/removal validation and persistence feedback are reused.

Application controls include Nika background, typography and sidebar artwork, page scale (70–125%, default 90%), background library/import/removal and pattern strength. The application preview renders a synthetic native controller without opening a window or accessing timer history. Clock controls keep their independent theme, format and text size; Nika links to Stopwatch appearance, while other clock themes retain text, outline and opacity controls and Pirate retains all nine transparency choices.

The native desktop now consumes page scale and application background choices. Scale waits until slider adjustment ends; oversized controller content remains scrollable. Floating clock/Todo/ring/hint overlays retain their own geometry. One background image is cached at a time, at most 1024 pixels per axis, with no upscaling. Missing/corrupt selections use the theme surface with feedback and preserve the selected custom ID.

Captures use the actual Avalonia renderer on a private Windows desktop with synthetic settings. `application.png`, `floating-clock.png`, `classic-clock.png`, `pirate-clock-more.png`, `compact.png`, and the scale/background captures are implementation evidence. Sample previews are fitted to the card.

Validation logs are in `.superpowers/sdd/2026-11-13-settings-appearance-native/`. No installation, application restart, commit or push was performed. Existing runtime artwork was reused.
