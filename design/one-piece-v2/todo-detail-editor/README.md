# Add and edit Todo details

This design covers the subtask and attached note editors opened from a task in the floating Todo overlay. The header names the action. A read-only parent task sits immediately above the writing area, so the destination stays visible when editing a long note.

The subtask editor uses a short multiline field. The note editor has room for two paragraphs before scrolling. Both put initial keyboard focus in the text field. Enter starts a new line, Ctrl+Enter saves, and Escape cancels. An empty or whitespace-only draft disables the primary action. Editing opens the existing text with a **Save changes** button.

| State | Approved preview | Implemented editor at 70%, 90% and 125% |
| --- | --- | --- |
| Add subtask, empty | [Design](native-preview/add-subtask-comparison.png) | [Compare sizes](implemented/add-subtask-comparison.png) |
| Edit subtask, long parent name | [Design](native-preview/edit-subtask-comparison.png) | [Compare sizes](implemented/edit-subtask-comparison.png) |
| Add note, Persian draft | [Design](native-preview/add-note-comparison.png) | [Compare sizes](implemented/add-note-comparison.png) |
| Edit note, two paragraphs | [Design](native-preview/edit-note-comparison.png) | [Compare sizes](implemented/edit-note-comparison.png) |

The implementation check opens each editor from the Todo overlay menu on a private Windows desktop with a temporary vault. It checks keyboard behavior, cancellation, retry after a failed save, and that saving preserves the parent and sibling details. Run it from the repository root.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true
New-Item -ItemType Directory -Force .superpowers/sdd/todo-detail-editor-live | Out-Null
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --todo-detail-editor-native -LogPath .superpowers/sdd/todo-detail-editor-live/native.log
```
