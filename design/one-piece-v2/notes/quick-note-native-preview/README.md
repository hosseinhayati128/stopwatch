# Quick Note — first-layer proposal

Design-only native Avalonia fixture, rendered on a private Windows desktop. The installed app and production NoteEntryWindow are unchanged.

- Logical size: 620 × 360; previews: 434 × 252 (70%), 558 × 324 (90%), 775 × 450 (125%).
- Existing shared panel frame and parchment/navy palette; no frame artwork changes.
- Header and close action, multiline writing area, fixed Cancel/Save note actions.
- Existing keyboard semantics: Ctrl+Enter saves, Escape cancels, Enter inserts a newline.
- No persistence or live settings are connected to the fixture.

Fixture: `tools/DesktopRegressionChecks/QuickNotePreview.cs`, mode `--quick-note-preview`.
Build passed with zero warnings/errors. Native checks passed for all three scales: window sizing, editor space, footer separation, and action bounds. All three images were visually inspected. Logs: `.superpowers/sdd/2026-11-16-quick-note-preview/`.

Saving, keyboard dispatch, error states and live integration remain for implementation after design approval.
