# Implement the Win+F2 Timer Command Mode

## Role

Implement the shortcut change described in this specification in the current Stopwatch Overlay repository. Work directly in the existing codebase, preserve all existing user work, and keep every edit strictly limited to keyboard-shortcut functionality.

## Project context

- The application is a .NET 10 WPF application for Windows.
- Global shortcuts currently use the Win32 `RegisterHotKey` API.
- The current defaults directly register `Win+F2` through `Win+F11` for individual actions.
- Existing shortcut behavior is primarily implemented in:
  - `StopwatchOverlay/AppSettings.cs`
  - `StopwatchOverlay/ControllerWindow.xaml`
  - `StopwatchOverlay/ControllerWindow.xaml.cs`
  - `StopwatchOverlay/ShortcutsWindow.xaml`
  - `StopwatchOverlay/ShortcutsWindow.xaml.cs`
  - `StopwatchOverlay.Tests/ShortcutSettingsTests.cs`
- The working tree may contain uncommitted user changes. Inspect and preserve them. Never revert, overwrite, discard, or broadly reformat existing work.

## Objective

Replace the ten separate global shortcuts with one global leader shortcut:

```text
Win+F2
```

Pressing `Win+F2` must enter a temporary timer-command mode. The next supported key selects an action.

## Command mappings

Use these fixed second-stage keys:

| Key | Action |
|---|---|
| `Space` | Start or stop the active timer |
| `R` | Reset the active timer |
| `O` | Show or hide the active timer overlay |
| `L` | Record a lap |
| `C` | Toggle the active timer between its current mode and Clock |
| `N` | Create a new timer and make it active |
| `T` | Select the next active timer |
| `X` | Close the active timer |
| `P` | Select, create, change, or clear the active timer's project |
| `D` | Open the project time dashboard |
| `Escape` | Cancel command mode without performing an action |

## Required behavior

1. `Win+F2` becomes the only global hotkey registered by default.
2. `Win+F2` must no longer create a new timer directly. Creating a timer becomes `Win+F2`, release the keys, then press `N`.
3. Command mode must work while another application has focus and while the stopwatch controller is hidden in the notification area.
4. Entering command mode must not permanently steal focus from the foreground application.
5. Command mode must automatically cancel after exactly 2 seconds if no command is entered.
6. Pressing `Win+F2` again while command mode is active must restart the 2-second timeout.
7. Modifier-only key presses must not select a command.
8. Letter commands must be case-insensitive and work regardless of Caps Lock or Shift state.
9. After a recognized command, immediately:
   - leave command mode;
   - remove any temporary keyboard capture;
   - execute exactly one action through the project's existing action method.
10. `Escape` must leave command mode without changing any timer state.
11. An unsupported non-modifier key must cancel command mode and show a brief `Unknown timer command` status message.
12. A consumed second-stage command key must not leak into the currently focused application. Suppress both its key-down event and matching key-up event. Do not suppress unrelated keyboard input outside command mode.
13. Handle key auto-repeat safely so holding a key cannot execute an action more than once.
14. Always release temporary hooks, timers, registrations, and UI resources when:
    - a command executes;
    - command mode times out;
    - command mode is cancelled;
    - the controller closes;
    - the application exits;
    - an exception occurs during setup.
15. Route actions through the same existing methods used by the old `WM_HOTKEY` handler. Do not duplicate or alter timer, project-history, overlay, or dashboard business logic.

## Implementation guidance

- Continue using `RegisterHotKey` for the `Win+F2` leader.
- Register only the leader globally during normal operation.
- For the next key, use a temporary, correctly scoped low-level keyboard hook or an equivalent mechanism that:
  - works when another application has focus;
  - does not activate the controller;
  - exists only while command mode is active;
  - suppresses only the consumed command key.
- Marshal all WPF and timer operations back to the application `Dispatcher`.
- Keep the hook delegate strongly referenced for the hook's lifetime.
- Do not add third-party packages.
- Keep the implementation small and isolated in shortcut-related code. A dedicated class such as `ShortcutCommandMode.cs` is encouraged if it makes lifecycle cleanup and testing clearer.
- Treat failure to register `Win+F2` as a recoverable shortcut conflict. Use the application's existing warning/status mechanisms and leave all other functionality operational.

## User feedback

When command mode starts:

- Update the controller's existing status area if the controller is visible.
- Show a small, temporary, topmost command hint when the controller is hidden.
- The hint must not activate itself or take keyboard focus.
- Display concise guidance such as:

```text
Timer command: Space Start/Stop · R Reset · O Overlay · L Lap
C Clock · N New · T Next · X Close · P Project · D Dashboard
```

- Close the hint immediately after a command, cancellation, or timeout.
- Keep its styling consistent with the existing dark application UI.
- Do not modify `OverlayWindow` or the timer overlay design to provide this feedback.

## Shortcut settings and migration

- The shortcut editor must configure only the command-mode leader.
- Its default must be `Win+F2`.
- It must display the fixed second-stage command map as read-only help.
- `Reset to defaults` must restore the leader to `Win+F2`.
- Continue detecting and reporting `RegisterHotKey` failure if `Win+F2` is already owned by Windows, an OEM utility, or another application.
- Do not claim that `Win+F2` is guaranteed to be available.
- Preserve all unrelated settings, including appearance, positioning, startup behavior, timer workspace, and project data.
- Existing settings containing the old `Win+F2`–`Win+F11` bindings must migrate safely to the new scheme.
- After migration, the old direct action shortcuts must not be registered or remain active.
- Do not delete or reset unrelated settings during migration.
- Add a narrowly scoped shortcut-settings schema/version value if needed.
- Preserve the numeric values of every existing `ShortcutAction` enum member if the enum remains in use.

## Labels and documentation

Update only shortcut-related captions, hints, tooltips, and documentation so they describe sequences such as:

```text
Win+F2 → Space
Win+F2 → D
```

Remove claims that `Win+F2` through `Win+F11` are separate global shortcuts.

## Testing requirements

Add or update focused tests covering at least:

- `Win+F2` is the default leader.
- Old direct global bindings are no longer defaults.
- Every command key maps to the correct `ShortcutAction`.
- Command lookup is case-insensitive.
- `Escape` cancels without an action.
- Unsupported keys do not execute an action.
- Timeout leaves command mode without an action.
- An action can execute only once per command-mode activation.
- Legacy shortcut settings migrate without changing unrelated settings.
- Existing `ShortcutAction` numeric IDs remain stable.
- Formatting the leader produces `Win+F2`.

Run:

```powershell
dotnet test StopwatchOverlay.sln
```

Also build the application if that command does not already perform a complete build. Resolve any failures introduced by this work.

## Strict scope restriction

Do not modify any feature or code unrelated to keyboard shortcuts.

### Permitted existing files

- `StopwatchOverlay/AppSettings.cs`
- `StopwatchOverlay/ControllerWindow.xaml`, but only shortcut-related labels, hints, or tooltips
- `StopwatchOverlay/ControllerWindow.xaml.cs`, but only shortcut registration, command dispatch, shortcut status, and lifecycle cleanup
- `StopwatchOverlay/ShortcutsWindow.xaml`
- `StopwatchOverlay/ShortcutsWindow.xaml.cs`
- `StopwatchOverlay.Tests/ShortcutSettingsTests.cs`
- `README.md`, but only shortcut-related passages
- `DEVELOPERS.md`, but only shortcut-related passages

### Permitted new files

- Shortcut-specific implementation files under `StopwatchOverlay/`
- Shortcut-specific test files under `StopwatchOverlay.Tests/`
- A shortcut-command hint window clearly named as a shortcut-specific component

### Explicitly forbidden

- Do not modify `TimerSession`, `TimerSessionManager`, workspace persistence, project-history logic, dashboard calculations, timer behavior, overlay behavior, themes, general window layout, packaging, release files, images, or unrelated tests.
- Do not rename, reorganize, broadly reformat, or refactor unrelated code.
- Do not update dependencies or target frameworks.
- Do not modify generated output or release directories.
- Do not revert, overwrite, discard, or clean up pre-existing user changes.
- Do not make opportunistic fixes, even if unrelated issues are discovered.
- If implementation appears to require a change outside the permitted scope, stop and explain why instead of making that change.

## Required workflow

1. Read this entire specification before editing.
2. Inspect the relevant existing files and the current Git diff/status.
3. State a concise implementation plan limited to the permitted scope.
4. Implement the feature while preserving existing work.
5. Review the final diff for accidental or unrelated edits.
6. Run the required tests and build.
7. Do not declare completion if tests fail because of your changes.

## Completion report

When finished:

1. Summarize the implemented behavior.
2. List every file changed or added.
3. Report the exact build and test results.
4. Mention any limitations, especially that another application can still reserve `Win+F2`.
5. Confirm explicitly that no unrelated project areas were changed.
6. Include a concise manual verification checklist.

