# Internet & Network — History & Export proposal

Native Avalonia design preview for visual review, not a production implementation. Uses the existing Settings shell, page scale, typography and shared frame. Counts, dates, paths and outcomes are synthetic. No real history is read or deleted, no Markdown is exported, no network request runs, and no installed binary or user setting changes.

## Proposed layout

Two cards continue the Connection & Monitoring layout. **Saved history** contains a count/date summary, existing retention choices (all, 30, 14 or 7 days), and a separate clear action. **Export to Markdown** contains the shared Obsidian folder, file name, Sync now and its result. The cards stack at narrow logical widths; the page scrolls while Done stays fixed.

The two-card arrangement is the proposed choice for review. A single form is simpler but mixes destructive history actions with exporting; a history dashboard adds charts and record browsing beyond this layer. No additional export scheduler, retention option or connection control is proposed.

## Behavior to carry into implementation

- Counts describe locally saved automatic checks. Manual Test now results are display-only.
- Keep all history remains the default. Current retention is applied during Markdown sync, after a destination is resolved; choosing a retention option must explain its effect on local history and the report. It must not imply deletion happens immediately on selection or without an export folder.
- The folder belongs to Obsidian / Markdown settings. Folder settings navigates there; Choose folder is the missing-destination recovery action. Preview buttons only change synthetic state.
- Empty file name uses Internet Log.md; add .md when absent. Accept a file name, not a path; implementation must validate platform-invalid/reserved names and keep the resolved file inside the selected folder.
- Sync can run with monitoring off. It imports recognized existing checks and preserves row notes from the selected report before rebuilding it from retained history. Empty local history is therefore not necessarily an empty exported report.
- Initial feedback is Not synced in this window, avoiding an invented last-success timestamp. Show pending, success, missing folder, write failure and empty-history states separately. A check timestamp is not an export timestamp.
- While syncing, disable repeat sync, destination edits, retention changes and clear. Keep operations off the UI thread in the eventual implementation.
- Clear opens an inline confirmation naming the saved-check count and the actual configured Markdown target. Cancel is focused first. It must require an explicit destructive action; merely opening it does not delete anything. Other files and monitoring preferences are unaffected.
- If no folder is configured, explain that only local checks can be cleared and existing Markdown copies cannot be updated. Never report both stores cleared if only one succeeded.
- Show partial failure explicitly and do not recommend an ordinary retry that could silently restore deleted entries. The preview demonstrates the distinction; recovery semantics need to be implemented together with the clear operation.

## Existing behavior requiring correction during implementation

`SettingsWindow.ClearInternetLogButton_Click` clears the local store and then calls ordinary `InternetLogSync.SyncLog`. That sync imports checks from the existing Markdown file before writing, so it can restore the history that was just cleared. The handler also ignores `InternetSyncResult.Success` and names the default file in its confirmation even when another filename is configured. Clear must be a dedicated coordinated operation that avoids re-import and reports each outcome accurately. This design-only change does not fix those production paths.

## Native evidence

`wide-70`, `wide-90`, `wide-125` show the 1120 × 720 window at the requested scales. Matching `-export` captures show the lower card at stacked sizes. Compact 840 × 600 examples cover all three scales. Additional states include confirmation, cancellation checks, partial clear, missing folder, invalid filename, empty history, sync progress/results, retention, long paths and enlarged text.

Run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-internet-export-preview` with an existing repository-local log directory. Checks cover six scale/window combinations, horizontal bounds, fixed footer, keyboard focus, sample action states, confirmation scope and zero settings-change events.

This is a visual proposal awaiting review. Production integration, real filesystem/network operations, Linux rendering, installation and Git publication are outside this preview task.
