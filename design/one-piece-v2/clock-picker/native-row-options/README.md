# Clock picker row and typography proposals

These are native Avalonia design previews, not the implemented picker UI. All three use the production shared Engraved Filigree frame at 460 × 480 with the same reference material, six clocks and 44-DIP rows. The existing compact-list layout is retained. Frame/material selection is outside this comparison.

1. **Navigator** — Almendra bold names, Roboto Condensed Black times and dark numbered keycaps. Recommended for consistency with the stopwatch.
2. **Clean** — Inter names and times with lighter keycaps and quiet dividers. The simplest treatment for frequent scanning.
3. **Logbook** — Playfair Display names, Cascadia Mono times, plain row numbers and subtle alternating bands.

In every option, Illustration is the active clock; Reading is the prospective keyboard selection. Running clocks use a teal triangle and paused clocks use amber pause bars. Every row stays fully readable. The shared header/footer are reference controls; only row typography, dividers and number treatment vary.

`row-options-01-03.png` is the comparison board. `option-01.png` through `option-03.png` show individual panels. `verification.json` records fonts, sizes, fixtures and measured content bounds. The board was inspected for all six names and times, consistent active/focus treatment, text clearance and matching frame geometry.

Reproduce by building `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then running `Run-HiddenDesktop.ps1 -Mode '--clock-picker-row-preview'` with a repository-local log path. The export ran successfully on a private desktop; no app windows, settings, shortcuts or production picker code were changed for these proposals. No new runtime images or fonts were added.

Navigator (option 1) was accepted in the following user response. State and edge-case proposals are saved in `../native-state-preview/`; their static renders do not replace native input verification.
