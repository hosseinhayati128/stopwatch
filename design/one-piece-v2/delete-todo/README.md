# Delete Todo — native design proposal

Implemented in the Todo overlay’s task menu. [Native capture of the implemented dialog at 90%](implemented/delete-todo-long-linked-90.png).

The confirmation uses the existing Todo visual identity: navy engraved header, selected emblem, parchment, native `TodoFrame` borders and `TodoVisuals` button styling. These previews use the default classic Todo appearance and synthetic tasks. They do not read settings, start sync services or invoke deletion.

## Layout and behavior

- A compact header identifies the action, with a centered close icon.
- A task card wraps the full task and shows whether a Telegram message is linked.
- Linked: explain that deleting removes the Obsidian todo, deletes its Telegram message and cancels unsent copies.
- Unlinked: show “No linked message” and explain that any unsent Telegram copy is cancelled.
- Both cases retain the conditional Habitica deletion explanation and the irreversible-action warning.
- Cancel is initially focused; Escape and the close icon cancel. Delete todo is a distinct muted-red action. The preview has no Delete handler.
- Long text has a bounded scrolling area, with the footer outside it. The sample task fits fully at each requested scale.

The overlay checks the Telegram chat and message ID again before deletion. If a queued copy was delivered or the link changed while the dialog was open, the app keeps the task and asks the user to review it again.

## Native previews

Every comparison sheet contains 70%, 90% and 125%, left to right. Captures retain their rendered pixel dimensions.

| Case | Comparison |
| --- | --- |
| Short task, linked | [Preview](native-preview/delete-todo-linked-comparison.png) |
| Short task, unlinked | [Preview](native-preview/delete-todo-unlinked-comparison.png) |
| Long task, linked | [Preview](native-preview/delete-todo-long-linked-comparison.png) |
| Long task, unlinked | [Preview](native-preview/delete-todo-long-unlinked-comparison.png) |

Short logical size: 540 × 430; long logical size: 540 × 500. Individual captures are stored beside the comparisons.

## Verification

On 20 November 2026, the preview fixture built with zero warnings and errors. All 12 native layouts passed dimension, complete sample text, panel bounds, footer separation, centered action labels and initial Cancel-focus checks. All four comparison sheets were visually reviewed.

The original design captures are under `native-preview`. To exercise the production dialog with a temporary vault and write captures to `implemented`, run the private-desktop launcher with `-Mode --delete-todo-native`.

Reproduce from the repository root, after creating the log directory:

```powershell
dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --delete-todo-preview -LogPath .superpowers/sdd/2026-11-20-delete-todo-preview/native.log
```

Only run the native preview through the private-desktop launcher when visible windows are prohibited.
