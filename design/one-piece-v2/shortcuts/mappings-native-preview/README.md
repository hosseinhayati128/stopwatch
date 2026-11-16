# Keyboard Shortcuts — Command Mappings proposal

Native Avalonia design preview, rendered on a private Windows desktop at 70%, 90%, and 125%. Uses the approved Main Shortcuts shell, shared frame and fonts, and real mapping fields/Clear handlers populated with synthetic defaults. No production source, live settings, global registration, installation, or Git publication changed.

## Layout

- Two command families: Timer commands (21) and Notes & Todos (5).
- Timer commands grouped into Timer controls, Projects & review, and Overlay & navigation. Existing actions and default keys are preserved.
- Three group columns at 70%; two at 90% with Overlay & navigation below; one at 125%. Long descriptions are available as tooltips rather than repeated below every action.
- The leader sequence reflects the selected family's draft leader. Escape is reserved for leaving command mode. Keys may repeat across the two families but must be unique within one family.
- Existing outer heading and Save/Cancel/Reset footer remain fixed. The inner selector, guidance, and groups scroll together. At 125%, scrolling is necessary in the existing 1120 x 720 window; this proposal does not redesign the approved outer shell.
- Reset retains its existing all-shortcuts scope. Edits remain drafts until Save.

## Captures

| Scale | Timer mappings | Later rows | Notes & Todos |
|---|---|---|---|
| 70% | [Timer](timers-70.png) | [Overlay](overlays-70.png) | [Notes](notes-70.png) |
| 90% | [Timer](timers-90.png) | [Overlay](overlays-90.png) | [Notes](notes-90.png) |
| 125% | [Timer](timers-125.png) | [Overlay](overlays-125.png) | [Notes](notes-125.png) |

At 90%: [Listening](recording.png), [duplicate key rejected](duplicate.png), [mapping cleared](unassigned.png).

## Verification and scope

Fixture-only build succeeded with zero warnings/errors. Native checks passed at all three scales: all 26 controls present, usable key widths, no horizontal overflow, scrolling, and fixed footer. Focus, duplicate rejection preserving the previous key, and Clear were checked using the real controls at 90%. All runs used Run-HiddenDesktop.ps1; no desktop switch or foreground test windows.

Sources: tools/DesktopRegressionChecks/ShortcutsMappingsPreview.cs and the dedicated --shortcuts-mappings-preview route in Program.cs. Build/native logs: .superpowers/sdd/2026-11-16-shortcuts-mappings-preview. Capture hashes and exact verification scope are recorded in verification.json.

Design only. Production integration, complete keyboard traversal, Save/Cancel/reset flows, non-Nika themes, and cross-platform behavior require implementation verification later. No search, new actions, new bindings, or dependencies were added. The installed application binary hash remains unchanged.
