# Clock picker: Navigator

Status: compact-list layout and Navigator typography chosen. The accompanying state refinements are ready for review in `design/one-piece-v2/clock-picker/native-state-preview/states-and-edge-cases.png`. This is a small, scoped UI change to the Nika picker; the shared frame infrastructure is already integrated.

## Intent and constraints

Make the clock picker fit the stopwatch's visual language while remaining quick to scan and operate by keyboard. Reuse the production shared frame and existing Nika dialog material. Add no per-page frame images, fonts, appearance settings or linked preferences. Preserve non-Nika appearance and the existing timer command routing. Run all UI verification on a private desktop. Installation and Git publication are separate from this design.

## Approaches

- Navigator: Almendra bold names, Roboto Condensed Black times, dark numbered keycaps. Recommended and accepted for consistency with the stopwatch.
- Clean: Inter names/times and quieter dividers; simpler but less consistent with the chosen ornamental treatment.
- Logbook: Playfair names, monospaced times and plain numbering; more of a printed ledger feel.

## Selected foundation

Keep the compact list, 44-DIP rows, right-aligned complete times, teal running and amber paused markers, and an ACTIVE badge belonging only to the current timer. Use the existing Engraved Filigree shared surface and its safe content insets; frame/material selection remains independent of overlay settings. Preserve the existing default window dimensions from the shared-renderer integration. Header, close control and footer use the same Nika visual family without changing close semantics.

## Proposed state refinements

- Hover adds a pale wash; it does not move selection or change the active timer. Selection uses a gold outline and warm fill. ACTIVE and selection can coexist on one row.
- Paused/unselected entries remain readable at full picker opacity. Overlay opacity defaults do not apply here.
- Mode labels below the time distinguish Stopwatch, Clock, Countdown and Timecode. Live clock entries use a clock glyph. Preserve the picker's existing time-value source and snapshot behavior; do not add a timer engine or frame-rate controls. Long elapsed hours must not wrap at 24 in the presentation.
- Names truncate with an ellipsis before times or ACTIVE badges. Preserve full names, state, category and separated status in row tooltips and accessible names. Use a suitable script font for RTL names while keeping time and row geometry left-to-right.
- Use a scrollable list. Digits and numpad 1–9 target their original list positions even when scrolled. Positions 10+ display plain numbers, not keycaps suggesting unavailable shortcuts. Selection remains visible when navigating by arrows.
- Keep single-click selection, Enter/double-click/Select clock confirmation, direct number selection and Escape/close cancellation. Retain the controller's guard against opening an empty picker.
- Buttons have normal, hover, pressed, keyboard-focus and disabled feedback without changing their dimensions. Selection and focus cues do not depend solely on color.

## Implementation boundary and verification

Apply the approved row presentation through a real list item template and native controls; the preview drawing class is not production input handling. Preserve timers' order, identifiers and activation behavior. Keep all existing theme resources and unrelated overlays intact.

Verify initial focus, arrows/scrolling, number/numpad selection, Enter, double-click, confirmation/cancellation, long/RTL names, 12+ entries, active-plus-selected state, all four modes, long hours, non-Nika behavior and stable geometry. Check actual control captures at the default and minimum supported sizes and explicit render scales. Static preview renders do not count as functional or physical-monitor-DPI verification.
