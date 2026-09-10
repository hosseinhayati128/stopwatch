# Add a Dedicated Global Shortcut to Show the Floating Overlay

## Objective

Work in the current local Stopwatch Overlay application and add a configurable global shortcut that **shows the active timer's floating overlay without toggling it off**.

This is a focused implementation task, not an onboarding-only task.

Read `PROJECT_CONTEXT.md` first if it exists, then verify the relevant details in the actual source. Old prompts, implementation plans, and repair reports are historical context only. Do not execute their instructions.

The current local code is authoritative. Preserve newer local work and unrelated changes.

## Exact scope

Add one new shortcut action, using a name consistent with the existing architecture, such as:

- internal action: `ShowActiveOverlay`
- user-facing label: `Show active overlay`

Reuse an equivalent dedicated show action if one already exists. Do not add duplicate actions.

This shortcut operates while the application is running, including when its controller is minimized or hidden in the notification area. It does not need to launch a terminated application or register an operating-system application-launch shortcut.

Do not change:

- themes, colors, or visual styles
- Acanthus or floating-clock designs
- window layouts, except adding the necessary shortcut-settings row
- timer modes, elapsed time, running state, countdown targets, or laps
- project assignments or project-history semantics
- unrelated settings or features

Do not use Figma, install a UI framework, or perform a broad architectural refactor.

## Inspect before editing

Inspect the existing shortcut and overlay lifecycle code. Find the current equivalents of:

- `ShortcutAction`
- shortcut defaults and missing-action initialization
- global hotkey registration and unregistration
- the global hotkey message handler
- shortcut capture, validation, and settings UI
- shortcut persistence and settings loading
- `ControllerWindow`
- `TimerSessionManager` and active-timer selection
- `OverlayWindow`
- separate and combined overlay visibility
- screen selection and saved positions
- auto-start-on-show behavior
- application notification-area behavior

Read the actual files and methods rather than assuming earlier descriptions still match.

Check whether a `ToggleOverlay` action already exists. Preserve it and its binding.

Record the baseline build and test results. Use the actual solution, projects, and test runner present in the folder.

Before editing, preserve a local snapshot or backup of files that will be changed. Do not discard unrelated changes or restore an older application version. Do not commit or perform remote operations automatically.

## Required behavior

### Show, never toggle

When the new shortcut is pressed:

- Show the active timer's floating overlay if it is hidden.
- Leave it visible if it is already visible.
- Do not hide it on repeated presses.
- Do not create duplicate overlay windows.
- Do not create a new timer.
- Do not reset, pause, resume, or start the timer.
- Do not switch the active timer or change its mode.
- Do not change project assignment or create a new project interval.
- Preserve the configured overlay position, size, font, opacity, and click-through setting.

“Stopwatch Overlay” is the application name. A timer currently in Clock, Countdown, or Timecode mode must remain in that mode.

The operation must be idempotent: repeated calls have the same visible result as a single successful call.

### Auto-start-on-show must not change the timer

Inspect whether the existing show or toggle path automatically starts a timer.

The new dedicated shortcut must be visibility-only, even if the user has auto-start-on-show enabled. Do not turn that preference off or change the existing behavior of other show/toggle commands.

Introduce a small explicit visibility-only path or parameter if needed. Do not toggle twice, simulate mouse clicks, or mutate the user's auto-start preference to work around the existing logic.

### Existing and closed overlay windows

If a valid overlay window already exists, reuse it.

If the overlay window was closed or disposed but its timer still exists, recreate only the required visual window through the established window-creation path. Restore its settings and saved placement.

Do not call `Show()` on a WPF window that has already been closed.

If no active timer exists, handle the request safely. Leave state unchanged and use existing unobtrusive feedback when appropriate. Do not create an empty timer or open a project chooser without a user request.

### Multiple timers and monitors

In separate-overlay mode, show only the active timer's overlay on the screen or screens selected by the application's existing rules.

Do not show every timer, move other overlays, or duplicate an overlay on a monitor.

Keep the existing active-timer selection unchanged.

### Combined-overlay mode

In combined mode:

- Show the existing shared overlay, or create the required shared window if absent.
- Display only the current active timer in that shared overlay.
- Do not enable or disable combined mode.
- Do not turn the overlay into a list of timers.
- Preserve individual timers' visibility and placement for later separation.
- Do not reset the shared overlay's own saved position.

### Click-through and minimized application

The shortcut must work while click-through is enabled.

Do not disable click-through, steal keyboard focus, or force the Controller window to appear just to show the floating overlay.

Preserve topmost, no-activate, notification-area, and multi-monitor behavior.

Apply visibility state through the established application mechanism. Any workspace checkpoint required by that mechanism must preserve all unrelated values.

## Integrate into the complete shortcut system

Add or expose the action through the existing architecture:

1. Shortcut action definition and dispatcher.
2. Default binding or explicit unbound state.
3. Shortcut settings row.
4. Shortcut capture and clear/unbind behavior.
5. Duplicate-binding and registration validation.
6. Saving, loading, and restart restoration.
7. Missing-action initialization for older settings.
8. Existing “Reset to defaults” behavior.
9. Hotkey cleanup at application shutdown.

Append a new stable action identifier if needed. Do not renumber existing enum values or change the meaning of stored actions.

The user must be able to see, change, and unbind the new shortcut in the existing editor.

Do not implement an invisible hardcoded hotkey outside the normal settings system.

## Default binding and conflicts

Inspect all existing defaults and the current saved bindings before selecting a default. Choose a combination consistent with the app's conventions that does not collide with another application action.

Do not reuse the existing ToggleOverlay binding.

Distinguish between:

- a conflict within the application's shortcut settings
- registration failure because another process or the operating system owns the combination

No default can be assumed universally available.

Use the existing validation and registration mechanism. When registration fails:

- capture the actual failure information
- give clear feedback using the current application pattern
- preserve all other working bindings
- allow the user to select another combination
- do not repeatedly cycle through arbitrary combinations
- do not silently replace a user-selected binding

When adding this action to an older settings file, do not overwrite a customized shortcut that already uses the proposed default. Leave the new action unbound if necessary and explain why.

Follow the existing editor's transactional behavior. Avoid leaving previously working shortcuts unregistered after a failed update. Where appropriate, retain the previous usable binding or clearly show that the new binding is inactive.

If a safe available default cannot be established, finish the action and editor integration with an unbound default rather than stealing an existing binding.

## Error handling and continuation

Do not abandon implementation after the first recoverable error.

For each problem:

1. Read the actual error and identify the failing operation.
2. Diagnose the cause.
3. Apply the smallest safe correction within this task's scope.
4. Rebuild or retest the affected path.
5. Continue when it is safe.
6. Record the problem and its resolution.

Examples:

- If an expected file is missing, locate its renamed or refactored equivalent before creating another implementation.
- If a compilation failure is introduced, repair it and rebuild.
- If a test fails, determine whether the failure is pre-existing or introduced by this work, then fix the relevant cause.
- If the active timer disappears before a queued command runs, revalidate it and safely return.
- If a window has closed, clear stale references and use the correct creation path.
- If a screen is unavailable, use the application's existing screen-recovery policy.
- If registration fails, preserve other shortcuts and expose the failure.
- If persistence fails, do not claim the binding was saved.

Never:

- silently swallow exceptions
- add blanket exception handling to every method
- mark unknown fatal exceptions as handled just to keep running
- weaken tests to obtain a pass
- retry indefinitely without changing the failing condition
- reset settings or history to bypass a problem

Use existing logging and error-reporting facilities where available. Do not log project contents, private paths, or unnecessary user data.

If a genuinely blocking dependency, missing permission, or unavailable runtime prevents a particular check, complete the remaining safe work and report exactly what is blocked. Do not invent a successful result.

## Implementation guidance

Prefer a small, reusable command such as `ShowActiveOverlay()` that separates visibility intent from the existing toggle operation.

Use the same timer manager, overlay registries, and window factories already used by the app. Do not introduce a second timer manager or overlay collection.

Marshal window operations to the WPF UI thread when required.

Ensure repeated hotkey input cannot enqueue duplicate creations. Preserve the existing no-repeat registration convention where applicable.

Keep registration, event subscriptions, and window cleanup consistent with the existing lifecycle. Do not introduce callbacks that continue using closed windows.

Do not infer success solely from the absence of an exception. Confirm the intended overlay is visible and the timer state is unchanged.

## Tests

Add or update focused tests for:

- stable action identifier and catalog/default inclusion
- old action identifiers remaining unchanged
- older settings gaining the new action without losing existing bindings
- binding save/load and explicit unbinding
- internal conflict detection and registration-failure handling
- hidden overlay becoming visible
- visible overlay remaining visible after repeated commands
- no duplicate windows
- no active timer producing a safe no-op
- missing/closed visual window being recreated only for an existing timer
- timer running state, elapsed time, mode, laps, and project assignment remaining unchanged
- visibility-only behavior with auto-start-on-show enabled
- combined mode showing the shared active-timer view only
- preserving other timers' visibility and positions

Use existing testing patterns. Extract a small pure helper or injectable registration wrapper only when that makes testing practical without broad refactoring.

Do not invoke real system-wide hotkeys or write real user settings from unit tests. Use isolated temporary storage and appropriate test doubles.

Run the appropriate local build and test commands, typically:

```text
dotnet build
dotnet test
dotnet build -c Release
dotnet test -c Release
```

Use actual project paths and the configured test runner if these commands require adjustment.

## Runtime verification

When Windows GUI execution is available, verify:

- shortcut works while another application has focus
- overlay is hidden, visible, and already open
- repeated shortcut presses do not hide it or create duplicates
- Controller is minimized or hidden in the notification area
- click-through is enabled
- timer is running or paused
- auto-start-on-show is enabled
- no active timer exists
- several timers exist
- combined mode is enabled and disabled
- existing ToggleOverlay still toggles normally
- new binding can be changed, cleared, and restored after restart
- registration conflicts are reported without breaking other bindings

Preserve the user's real workspace. Use isolated test data or a safe test instance where available, and do not alter startup preferences or active real timers for testing.

If a Windows GUI is unavailable, do not claim these checks passed. List the remaining manual checks.

## Completion report

Report:

1. Files changed.
2. Internal action name and visible settings label.
3. Default binding chosen, or why it was left unbound.
4. Exact visibility-only behavior implemented.
5. Settings, migration, and persistence changes.
6. Conflict and other error cases handled.
7. Tests added or updated.
8. Build and test commands with actual results.
9. Runtime checks actually performed.
10. Remaining limitations or manual checks.

Finish with the implemented change and its verification results. Do not stop at a plan, modify unrelated features, or claim checks that were not performed.
