# Timer Editor → Close Timer: one-confirmation design

Native Avalonia design proposal, using the existing Timer Editor, approved Close Timer dialog, typography and shared frame. Synthetic timer data only; no live controller or saved settings are loaded. This is a visual spike, not production integration.

## Proposed flow

1. In the editor's expanded **Project records** section, rename **Delete Timer** to **Close Timer…**. Keep it distinct from **Discard records**, with the clarification that closing the timer keeps saved project records.
2. Open the approved **Close Timer** confirmation once. Identify the actual target timer with its project name, running/paused state and elapsed time. Add the editor-specific consequence and an explicit note that unsaved editor changes will not be applied.
3. **Cancel**, **Esc** and **X** return to the same editor, preserving its draft. **Close Timer** closes that timer and the editor, without another confirmation. Saved history and other timers remain intact.

Cancel retains visual emphasis and initial focus. Confirmation should follow the focused button; an unfocused Enter must not trigger closure. The confirmation summary uses the actual timer identity and an opening snapshot of its time, not an unsaved project rename or duration adjustment in the editor. Pending record-discard edits are not applied by closing.

## Layout and cases

The existing 500-logical-pixel width is retained. The editor-specific dialog is 380 logical pixels tall to accommodate the draft warning without crowding its footer. The timer name wraps; the time remains right-aligned. No frame artwork or clock overlay is changed.

| Case | Sample | 70% | 90% | 125% |
| --- | --- | --- | --- | --- |
| Running | Programming, running stopwatch, 00:44:27 | 350 × 266 | 450 × 342 | 625 × 475 |
| Paused | Programming, paused stopwatch, 00:44:27 | 350 × 266 | 450 × 342 | 625 × 475 |
| Long name | Programming · Desktop interface improvements, running stopwatch, 00:44:27 | 350 × 266 | 450 × 342 | 625 × 475 |

`close-timer-editor-flow-90.png` places the native editor entry and its proposed confirmation side by side, with the proposed outcomes labelled outside the panels. The editor is scrolled to its expanded Project records section. Each case also has three individual PNGs and a comparison sheet at their original pixel sizes.

## Implementation boundary

Currently `TimerEditorWindow.DeleteTimerButton_Click` shows a generic Delete Timer prompt and returns `DeleteTimerRequested`; the controller then calls `CloseSession`, which presents the approved Close Timer prompt. Integration must replace this two-prompt route with one confirmation. The preview does not change either handler or simulate timer closure.

Reuse the existing confirmation and timer-removal lifecycle. After a confirmed editor close, remove the captured session once without calling another prompting entry point. Re-check that the target still exists, and retain overlay cleanup and timer-list refresh. Cancellation must leave the editor open and its draft unchanged. Other routes to Close Timer must keep their confirmation.

## Rendering and checks

Fixture: `tools/DesktopRegressionChecks/TimerEditorClosePreview.cs`, mode `--timer-editor-close-preview`. Run only through `Run-HiddenDesktop.ps1` on a private Windows desktop, without switching the active desktop. The fixture modifies only the synthetic native control instances and never invokes editor save/delete or controller actions.

The fixture build passed with zero warnings/errors. All nine dialog cases passed native dimensions, complete text, no-scroll layout, identity/state, initial Cancel focus, centered button labels and footer separation checks. The native editor entry passed action/hint visibility and unchanged synthetic timer/result-state checks. All three comparison sheets and the flow sheet were visually inspected.

Validation evidence is recorded locally in `.superpowers/sdd/2026-11-19-timer-editor-close-preview/`. Production flow integration, timer-removal behavior, installation and publication are outside this design task.
