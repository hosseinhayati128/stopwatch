# Close Stopwatch — implemented first layer

The real CloseActionDialogWindow now uses the approved shared-frame Nika design. Logical size is 500 × 428; the title and Cancel footer stay visible while the actions and preference scroll when larger text or a smaller working area requires it. Normal text fits without scrolling at 70%, 90% and 125%. The shared frame artwork and clock overlays are unchanged; legacy themes keep their layout.

The existing Minimize, Exit and Cancel handlers are reused. Enter is handled before focused controls and always chooses Minimize; Escape, Cancel and the top-right X cancel closing. Space still activates the focused button. Remember my choice starts unchecked, updates its explanation when toggled, and takes effect only after Minimize or Exit. Duplicate controller close requests share the pending prompt, and completed dialogs ignore repeated choices.

The controller passes current settings and preserves its existing hide/shutdown paths. Preview controllers update only their synthetic settings, with no settings-file write. Ownerless and modal completion, owner closing, owner-centered placement and working-area limits are covered by the native fixture.

## Verification

`--close-stopwatch-native`, executed only through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` on a private Windows desktop, passed all five groups:

- Native sizing at 70%, 90%, 125%; readable primary hover/pressed states; normal text without scroll; 125% text, live page zoom and constrained 800 × 600 screen at Windows 125% scaling.
- Enter/Escape from focused controls, Space activation, all action buttons, ordinary close, repeated result selection, and legacy themes.
- Modal and ownerless results, including the legacy explicit-theme async call; Cancel cannot return a remembered action; owner closing leaves no orphan; owner-centered placement.
- Synthetic controller duplicate-close handling, canceled and one-time choices, running timers preserved when minimizing, remembered Minimize skipping the next prompt and remembered Exit completing once.
- Existing Pirate brass-button templates.

Build completed with zero warnings and errors. Six PNGs in this directory were rendered from production controls and visually inspected. Local build logs, verification output, source hashes and earlier-work baseline are retained in `.superpowers/sdd/2026-11-18-close-stopwatch/`.

The installed application was not updated or restarted. Live tray/services/global shortcut shutdown, real settings-file persistence and physical multi-monitor transitions were not exercised; the focused checks use synthetic settings and the real close handler. No commit or push is part of this implementation request.

Independent review found one unnecessary settings read in that legacy explicit-theme call; it is fixed. A minor suggestion for stronger scroll-position assertions is deferred; constrained scrolling was visually checked. The final reviewed build and all five native groups pass.
