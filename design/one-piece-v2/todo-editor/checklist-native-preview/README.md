# Expanded Todo checklist

The Add / Edit Todo editor keeps the checklist title, completion count and **Add subtask** together. Each row has a checkbox, a wrapping editable field, Move up, Move down and Remove. The first and last rows disable the direction they cannot move. A checked row keeps its text readable and editable.

Saving a blank subtask opens the checklist, focuses that field and shows **Enter a subtask or remove this row** underneath. Typing a subtask clears the message. The existing editor footer stays visible as the body scrolls.

[Compare all three states in Classic and Pixel at 90%](checklist-overview.png)

| State | Classic at 70%, 90%, 125% | Pixel at 70%, 90%, 125% |
| --- | --- | --- |
| Empty | [View sizes](classic-empty-comparison.png) | [View sizes](pixel-empty-comparison.png) |
| Completed item and long text | [View sizes](classic-mixed-comparison.png) | [View sizes](pixel-mixed-comparison.png) |
| Blank subtask after Save | [View sizes](classic-validation-comparison.png) | [View sizes](pixel-validation-comparison.png) |

These are the approved native design captures. The [implemented checklist](../README.md#expanded-checklist) uses the same sample states and includes the command for checking the production controls.
