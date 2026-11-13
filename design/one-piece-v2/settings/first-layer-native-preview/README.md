# Settings & Overlay Inspector — first-layer proposal

Native Avalonia design spike for the shared Settings shell, category navigation, Overlay & Position page and live preview. This is a proposal for visual review, not production implementation. It uses synthetic settings/displays, the existing `StopwatchFrameSurface` and the actual `ThemedClockFace` renderer. No new runtime artwork or dependencies are added.

## Proposed layout

- A standalone shared engraved frame, familiar parchment/navy colors, heading and close control. Done remains fixed in the footer with the existing immediate-apply behavior; no new Save, Cancel or Reset flow.
- Left navigation retains all 13 existing categories, grouped visually as Overlay, Application and Connections. Category names stay unchanged. Only Overlay & Position is designed in this pass; the other category content is outside scope.
- At 1120 × 720 DIP, the editable settings occupy the middle column and a live preview card occupies the right. This puts navigation first while keeping the effect of a setting beside its control.
- Preserve the Display selector (All displays plus connected screens), all seven Position choices, Click-through, Hide from screen capture, and Show / hide active overlay. Supporting text explains click-through and that a capture-hidden overlay remains visible on the user's screen.
- The preview contains the actual clock, a separate placement diagram, and interaction/capture summaries. The diagram communicates a preset rather than physical screen scale. Custom placement does not invent an exact position; its caption refers to the user's saved overlay position.
- At 840 × 600 DIP, a short preview strip sits above Display and Position fields arranged side by side. Both fields are visible immediately. Interaction controls continue below in the same scroll area; category navigation scrolls independently and Done remains fixed.
- Long display labels use ellipsis and a complete tooltip. The sample long-name state also exercises the clock renderer with a Persian project name; font shaping remains the renderer's existing behavior.

## Approach and scope

This proposal moves navigation ahead of the preview, rather than keeping the current preview/navigation/form ordering. A preview above the entire form is used only at compact widths, where three columns would squeeze the controls. These are proposed design choices for the user's review, not previously approved implementation requirements.

Appearance, fonts/colors, Todo appearance, behavior and integration pages remain later layers. Existing appearance profiles and the approved frame renderer are reused. No physical overlay movement, click-through window flags, capture exclusion, real monitor enumeration, settings persistence, app restart or Git publication occurs in this preview.

Before implementation, verify the native settings callbacks as well as layout. Inspection found that the Desktop Settings page reads/writes ScreenIndex and Position, while the Desktop controller's settings-change handler currently applies overlay appearance without a corresponding screen/preset placement path. Settings also exposes ShowOverlayRequested, but SettingsButton_Click currently does not subscribe to that event. The WPF controller has separate screen/placement logic; it is not evidence that the Desktop controls work. Any approved implementation must connect and test the intended actions without silently moving overlays during design review. Capture/click-through and multiple-monitor behavior require their own checks at that stage.

## Captures and validation

- `overview.png`: wide layout with the default single-display/top-right proposal.
- `interaction.png`: click-through and capture hiding selected.
- `all-displays.png`: two synthetic displays and a bottom-center preset.
- `custom-position.png`: custom placement without a fabricated coordinate.
- `long-name-rtl.png`: long monitor label and Persian sample project.
- `compact.png`: compact initial view with both placement fields visible.
- `compact-controls.png`: compact scrolled view of the interaction controls.
- `preview-hidden.png`: local sample preview hidden by its demonstration button.

The private-desktop `--settings-shell-preview` mode passed all six fixture states. Minimal checks cover category/position counts, field widths, no horizontal overflow, header separation, placement-marker bounds, local sample updates and fixed footer placement. The harness build completed with zero warnings/errors. These checks do not validate production settings behavior. `verification.json` records native dimensions and the fixture scope.

The preview's selectors, checkboxes and Show/Hide button update only local sample state; other category buttons demonstrate navigation styling. Production Settings code, the installed executable, and unrelated Todo artwork are unchanged.
