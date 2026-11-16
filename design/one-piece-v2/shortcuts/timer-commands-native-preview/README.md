# Timer Commands popup — native design proposal

Design-only Avalonia fixture using the existing shared frame, palette and bundled fonts. No production popup implementation, global hotkey registration, settings writes, app restart or Git publication.

The compact 920 x 490 logical layout shows all 21 existing timer commands in three groups: Timer controls, Projects & review, and Overlay & navigation. Key badges sit beside concise action labels. The leader appears at the top; Escape guidance and a small active-timer context label explain the current mode. Programming is synthetic sample context. Badges are informational, not new clickable action buttons.

Native previews: timer-commands-70.png, timer-commands-90.png, timer-commands-125.png. These use 70%, 90%, and 125% scaling, without scrolling. custom-90.png demonstrates Ctrl+F8 as leader, F9 for Start/stop, and Reset omitted when unassigned. Default bindings come from AppSettings; production integration should use the user's current mappings, leader and active timer.

Verification: fixture build succeeded with zero warnings/errors. All four native renders passed command counts, row bounds and desktop-fit checks via Run-HiddenDesktop.ps1, entirely on a private Windows desktop. Images inspected visually. Logs: .superpowers/sdd/2026-11-16-timer-commands-preview.

Pending implementation verification: real command dispatch, focus/keyboard capture, dismissal/chaining, live timer changes, screen-edge positioning, smaller work areas, long/custom key labels, other themes and cross-platform behavior. Preserve the existing shortcut lifecycle. No countdown, new commands, settings or animation are proposed.
