# Delete subtask and Delete note

The Delete action on an expanded Todo opens a confirmation for the selected subtask or attached note. The dialog shows the selected detail first and its parent task below. Cancel receives initial focus. The destructive button names the item it removes.

Both cases use the Todo overlay’s classic appearance. The dialog grows with the detail text up to a bounded scrolling area. The parent task and action buttons sit outside that area.

| Example | Design at 70%, 90% and 125% | Implemented dialog at the same sizes |
| --- | --- | --- |
| Subtask — Sketch layout | [Design](native-preview/delete-subtask-comparison.png) | [Overlay capture](implemented/delete-subtask-comparison.png) |
| Long attached note — illustration proof | [Design](native-preview/delete-note-comparison.png) | [Overlay capture](implemented/delete-note-comparison.png) |

The preview uses synthetic items. To reproduce from the repository root, build the preview fixture and capture it on a private Windows desktop.

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true
New-Item -ItemType Directory -Force .superpowers/sdd/delete-todo-detail-preview | Out-Null
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --delete-todo-detail-preview -LogPath .superpowers/sdd/delete-todo-detail-preview/native.log
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --delete-todo-detail-native -LogPath .superpowers/sdd/delete-todo-detail-preview/implemented.log
```
