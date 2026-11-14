# Behavior native design preview

The proposal keeps Behavior in the Settings & Overlay Inspector shell. Four tabs group the controls so each task has its own view. At narrower sizes the cards stack and scroll beneath the tabs. The sidebar and Done footer stay in place.

- **Timer** groups startup, REC, colon blink, and countdown input with a clock sample. The keyboard timing slider updates the example beside it.
- **Idle stop** pairs defaults with the selected project’s timeout and the list of project rules. The list shows whether each rule is on and the timeout it uses.
- **Focus tracking** pairs the pause thresholds with a sample resume prompt. The sample updates when tracking is turned off, a pause falls below the minimum, or the suggested response changes.
- **Project categories** puts the add field beside the lists of built-in and custom categories. The duplicate-name example shows the validation beside the field.

The fixture uses native Avalonia controls and synthetic projects. It supplies the initial focus values and project selection that the current desktop page does not load. Before integration, verify the runtime wiring for startup, countdown input, colon blink, idle rules, and focus prompting. Background timer reminders belong to the legacy WPF app. Category changes in the preview use an in-memory model.

The private desktop run checked all four tabs at 1120 × 720 and 840 × 600, each at 70%, 90%, and 125% UI scaling. It checked horizontal fit, fixed footer position, the sample idle rule, focus on and off, and duplicate category feedback. The compact capture is scrolled to the project rules.

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-behavior-preview -LogPath .superpowers/sdd/2026-11-14-settings-behavior-preview/native.log
```
