# Timer Editor — single Close Timer confirmation

Implemented the approved editor flow using the existing native Close Timer dialog and timer-removal lifecycle.

- The editor action is **Close Timer…**, with a saved-history clarification beside the project-record controls.
- One owned modal identifies the actual timer, its current state and its opening time snapshot. Unsaved name/time/running edits are not substituted into the summary.
- **Cancel**, **Esc**, **X** or native window close return to the same editor with its draft, invalid values and pending record changes intact. Focus returns to the Close Timer action.
- Confirming closes that timer and its editor once. Saved history and other timers remain unchanged; draft edits are not applied.
- Duplicate activation is guarded. Closing the editor/controller closes the modal. A late or stale confirmation cannot remove another timer.
- Other Close Timer entry points retain their confirmation. No new timer loop, setting, frame asset or dependency was introduced.
- Midnight, Daylight and Pirate retain their native appearance and include the same mode/state and elapsed/remaining/current-time meaning in the confirmation text.

The editor-specific dialog is 500 × 380 logical pixels; non-editor dialogs retain their existing size. The page-scale update path retains the editor height. Native running, paused and long-name cases at 70%, 90% and 125% match all nine approved PNGs byte for byte. The extra RTL/150%-text capture demonstrates reaching the final warning through the scrollable body; the footer remains available.

## Native evidence

`TimerEditorCloseNativeChecks` exercises the real synthetic controller → editor → modal → manager/overlay lifecycle, without loading user settings or invoking live services. Capture files in this directory come from that production flow, not the design prototype.

- `close-timer-{running,paused,long-name}-{70,90,125}.png`: production dialog matrix.
- `close-timer-{running,paused,long-name}-comparison.png`: comparisons at original pixel dimensions.
- `editor-entry-90.png`: actual editor, scrolled to Project records.
- `close-timer-large-rtl-125.png`: 150% typography, RTL project name, body scrolled to the draft warning.

The regression first reproduced the old second-confirmation behavior. After integration, five new native groups, four standalone Close Timer groups and fourteen existing editor/shortcut groups passed. The full unit suite passed 1,117/1,117 with no failures or skipped tests. All UI checks and the unit suite ran through `Run-HiddenDesktop.ps1`, which never switches the active desktop. Native and unit-project builds completed with zero warnings/errors. Detailed logs and hash receipts are retained in `.superpowers/sdd/2026-11-19-timer-editor-single-close/`.

The implementation is not installed or published by this task. The existing application process is left running its installed build.

Independent review found one legacy-summary omission. A new native regression reproduced it, the message now shares the mode/state and time caption with Nika, and the expanded modes/themes group passed. The full unit suite and affected native close groups were rerun after the correction. No review findings remain deferred.
