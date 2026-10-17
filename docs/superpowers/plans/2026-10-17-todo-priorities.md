# Todo priorities, ordering, and hold completion

Approved UI: four levels (1 Urgent/red, 2 High/orange, 3 Normal/blue, 4 Low/gray), compact clickable badge at the left of each task, Priority/Manual sorting, drag handles in Manual mode, completed tasks last, default Normal. Preserve each mode's saved ordering. Completion and uncompletion require a one-second hold with visible progress; releasing early, leaving, scrolling, hiding, or losing focus cancels.

1. Add `TodoOrganizationStore` and isolated regression checks. Store preferences in the configured notes folder's `.stopwatch-todos.json`, leaving task Markdown and Telegram identities unchanged. Scope to vault/subfolder; save atomically, preserve unknown/stale identities, fail safely on corrupt data. Arrange incomplete then completed tasks with priority or saved manual order.
2. Add `HoldToCompleteCheckBox` with a monotonic one-second threshold, cancelable pointer/Space-key interaction and observable progress. Ordinary click/automation invocation cannot complete tasks. Verify short hold, full hold, cancellation, and exactly-once completion.
3. Integrate priority menus, a sort selector, manual drag handles and keyboard move actions, and hold progress in TodoOverlayWindow. Preserve normal task formatting, Edit actions, filtering, polling, and Telegram completion behavior. Avoid polling rebuilds during holds or drags.
4. Run core and native regressions, inspect representative themes with dummy tasks, review changes, publish and install. Do not send Telegram test messages or modify personal tasks during tests.
