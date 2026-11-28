# Stopwatch desktop pet

Draft for review, November 28, 2026.

## Goal

Give Stopwatch Overlay an optional animated desktop companion that reacts to actual work and provides quick access to timer controls and Todos.

## Placement

The clock-attached option would put the pet beside the selected floating clock and move it with that clock. The chosen option is an independent floating window. Drag it anywhere on the desktop and the stopwatch remembers the drop position across restarts and when the pet is hidden and shown again. Timer selection, clock placement, and minimizing the controller to the tray are independent of the pet’s position and visibility.

Use the Todo launcher's drag behavior and the shared screen constraint as references. Save the position after a drag settles. If the saved position is outside the current desktop, show the pet in a reachable part of the nearest screen and keep the saved position until the user moves it again.

## Proposed first version

| Action or event | Pet response |
| --- | --- |
| Left-click | Play a short greeting. A drag moves the pet. |
| Right-click | Show the selected timer or project name and offer **Start**, **Pause**, or **Resume** as appropriate, **Switch timer**, **Open Todos**, and **Hide pet**. Use the existing app actions. |
| At least one work timer running | Play a quiet working animation. |
| No work timer running | Rest. A running timer still counts while its display is switched to the wall clock. |
| A running countdown reaches zero or a todo is successfully checked off in the app | Celebrate once, then return to the current working or resting animation. |

Use one pet per app instance. Start with the pet hidden and add a show/hide control to the existing tray menu. Remember visibility as well as position. Keep the enabled pet visible when the controller is minimized to the tray and close it when the stopwatch exits. Use a consistent sprite footprint so animations leave the pet in place. The character artwork is open for review in a visual preview.

## App integration

Build the window in the Avalonia desktop app. Store the pet's position and visibility with the existing app settings. Read running timers from the controller's session manager and trigger celebrations at the existing countdown completion and successful local todo completion points. Reuse the controller's commands so selecting, starting, or pausing a timer behaves the same from the pet, the overlay, and the keyboard. Keep animations silent and stop rendering them while the pet is hidden.

## Acceptance checks

- Drag to another monitor, change the selected timer, hide the floating clock, and minimize the app to the tray. The pet stays at the drop position.
- Hide and show the pet, restart the app, and disconnect a monitor. The visibility preference is remembered and the pet stays reachable.
- Start, pause, and switch timers from the pet and via hotkeys. The menu names the correct timer and the pet rests only after the last work timer pauses or finishes.
- Let a countdown finish and complete a todo. Each success gives one celebration. Clicking the pet gives a greeting and dragging moves it without a greeting.
