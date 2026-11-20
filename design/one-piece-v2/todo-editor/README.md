# Add and Edit Todo · first layer

The task text gets the first focus. Add places priority and manual position side by side. Edit gives priority its own row and opens a saved deadline with its local date and time. Save and Cancel sit below the scrollable body.

Checklist opens the existing checklist editor and shows the number of subtasks. A blank subtask reopens the checklist on Save so the user can correct it. The examples use an empty new task and an existing print task with a deadline and two subtasks.

| Native comparison | Add without a deadline | Edit with a deadline |
| --- | --- | --- |
| Classic design | [Preview](native-preview/classic-add-comparison.png) | [Preview](native-preview/classic-edit-comparison.png) |
| Classic implementation | [70%, 90%, 125%](implemented/classic-add-comparison.png) | [70%, 90%, 125%](implemented/classic-edit-comparison.png) |
| Pixel design | [Preview](native-preview/pixel-add-comparison.png) | [Preview](native-preview/pixel-edit-comparison.png) |
| Pixel implementation | [70%, 90%, 125%](implemented/pixel-add-comparison.png) | [70%, 90%, 125%](implemented/pixel-edit-comparison.png) |

Build DesktopRegressionChecks and run its hidden desktop launcher with `--todo-editor-native` to capture the production editor. Each case uses a temporary vault and checks saved priority, local deadline, manual order, checklist contents, or cancellation.
