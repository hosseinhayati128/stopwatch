# Settings & Overlay Inspector first layer

Spec: `design/one-piece-v2/settings/first-layer-native-preview/README.md` and its approved native captures.

Implement the Nika standalone shell, grouped navigation, responsive placement and interaction page, and actual preview. Retain all thirteen settings pages and immediate persistence. Connect active-overlay visibility and display/preset placement, including mirrors for All displays and saved custom positions. Reuse the shared renderer, form styles, timer workspace position fields and existing overlay operations.

## Constraints

- Keep the existing checkout and unrelated Todo PNG changes intact. No installation, restart, commit or push in this task.
- Every native window and test runs on the private desktop; fixtures use synthetic settings and controller constructor, never live stores or platform services.
- No new artwork, packages, settings schema or continuously running preview timer.

## Task 1: native shell and control wiring

Files: SettingsWindow.axaml.cs, SettingsWindow.TodoAppearance.cs, new SettingsWindow.Navigator.cs, ControllerWindow.axaml.cs, new ControllerWindow.OverlayPlacement.cs, OverlayScreenConstraint.cs; regression Program.cs and SettingsShellNativeChecks.cs.

Interfaces: existing SettingsChanged flags, ShowOverlayRequested, NavigationList/category keys, ReportPersistenceResult; controller overlay state/visibility methods and TimerSession custom-position fields.

1. Add native checks for standalone frame/compact geometry, category navigation, persistence failure, no load writes and active overlay toggle. Build and run with `--settings-shell-native` privately. Expected: fail because shell and controller wiring are missing.
2. Implement the approved shell with the actual controls and reused preview renderer. Retain legacy page content and other themes. Wire ShowOverlayRequested and targeted placement changes; mirror the same logical timers across requested displays, preserve custom positions, clamp by working area/DPI, clean up mirrors.
3. Extend native checks with synthetic screen geometry, controller visibility/placement/mirror lifecycle, all categories, resizing, controls and error states. Run privately. Expected: all checks pass and production PNGs generated under `design/one-piece-v2/settings/implemented`.
4. Build/run the full unit suite privately and relevant native regression suites. Expected: all pass; audit-network warnings reported separately.
5. Fresh read-only final reviewer, then one bounded fix pass for important findings with RED/GREEN checks. Preserve evidence and leave changes uncommitted for user review.

## Review Focus

- Switching categories and themes retains all old fields, callbacks, previews and selection; no duplicate parents or detached controls on resize.
- Custom placement persists per timer/display, presets account for work area and DPI, and monitor changes do not cause duplicates or repeated allocation on timer ticks.
- Mirrors share logical timer operations and visibility, close cleanly, and do not duplicate timer data or settings writes.
- Initial loading, synthetic tests and preview controls cannot mutate live settings, history, installed app or real desktop.
- Footer persistence errors remain visible and readable at compact size; controls remain keyboard accessible.
