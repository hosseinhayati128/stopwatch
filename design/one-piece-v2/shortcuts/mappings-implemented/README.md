# Command Mappings — implemented native views

The approved design is integrated into the Nika Keyboard Shortcuts window. Timer controls, Projects & review, and Overlay & navigation contain the existing 21 timer actions; Notes & Todos contains the existing five note actions. Original controls, capture/Clear handlers, draft dictionaries, default bindings and Save/Cancel/reset scope are retained. Non-Nika themes retain their existing layout.

Examples follow the current draft leaders and Quick note key, including unassigned states. Duplicate and reserved-key feedback remains within the relevant family, and Save reveals the invalid family when needed. Tab and Shift+Tab move focus without changing a mapping. Other capture and duplicate rules remain unchanged.

The groups use three columns at 70%, two at 90%, and one at 125%, within a scrolling body and fixed footer. Frame fitting is restricted to this window, following client resize and page-scale changes. It fixes retained size and centering at the minimum window size; clock overlay sizing is unchanged.

## Native captures

| Scale | Timer mappings | Later rows | Notes & Todos | Minimum window |
|---|---|---|---|---|
| 70% | [Timer](timers-70.png) | [Overlay](overlays-70.png) | [Notes](notes-70.png) | [700 x 500](minimum-70.png) |
| 90% | [Timer](timers-90.png) | [Overlay](overlays-90.png) | [Notes](notes-90.png) | [700 x 500](minimum-90.png) |
| 125% | [Timer](timers-125.png) | [Overlay](overlays-125.png) | [Notes](notes-125.png) | [700 x 500](minimum-125.png) |

[Listening](recording.png) · [Duplicate rejected](duplicate.png) · [Custom leader with unassigned Quick note](unassigned.png).

These are actual production controls rendered in an isolated Avalonia app with synthetic defaults/drafts. All GUI and unit checks ran through Run-HiddenDesktop.ps1 on a private desktop, with no desktop switch. The preview fixtures do not persist changes to live settings.

## Verification

- Desktop/native fixture build: succeeded, zero warnings/errors.
- Unit build: succeeded, zero warnings/errors; full suite: 1,117 passed, zero failed/skipped.
- Native mappings: four groups passed, covering scale/scroll/footer, minimum-window and live-scale frame fit, both command families, capture, modifiers/Escape, numeric key normalization, duplicates, Clear, custom/unassigned examples, Reset, Save, Cancel, input-map isolation, actual Tab/Shift+Tab focus movement, and Daylight compatibility.
- Main Shortcuts regression: passed at 70/90/125%, including capture, duplicate globals, validation routing, reset, Save/Cancel and Escape.
- Existing timer/note routing checks: passed, including command leaders, custom mappings, chaining, pickers, overlays and cancellation.
- Archived design preview: passed; its PNG hashes remained unchanged.
- Independent read-only review: no actionable production findings. Its focus-verification limitation was resolved with passing focus-movement assertions.

Logs and red/green evidence are in .superpowers/sdd/2026-11-16-shortcuts-mappings. verification.json records exact capture/source hashes and test scope. The installed binary remained unchanged. Not installed, restarted, committed or pushed by this task; cross-platform rendering was not checked.
