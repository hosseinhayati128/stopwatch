# Timer Commands — smaller native proposal

Revision after the user identified the first design as too large for the Win+F2 command hint. Win+F3 uses the separate Note Commands popup and is not changed here.

The logical size is now 800 x 280, compared with 920 x 490 in the first proposal: about half the area. Native captures are 560 x 196 at 70%, 720 x 252 at 90%, and 1000 x 350 at 125%. All 21 commands remain visible. Command-label font size remains 13 logical units; reduced header size, padding, row gaps and decoration provide the size reduction. The surface retains the existing parchment/navy/gold palette with a thin border and subtle group dividers.

Captures: timer-commands-70.png, timer-commands-90.png, timer-commands-125.png. custom-90.png shows a synthetic Ctrl+F8 leader, F9 Start/stop binding, and omitted unassigned Reset action. Programming is synthetic active-timer context. Key badges are informational.

The TimerCommandsPreview fixture now renders this revision through --timer-commands-preview. The earlier screenshots remain in timer-commands-native-preview for comparison. Fixture build passed with zero warnings/errors; all four private-desktop renders passed command-count, row-bounds/footer-overlap and desktop-fit checks, and were visually inspected. Logs: .superpowers/sdd/2026-11-16-timer-commands-compact-preview.

Design only. No production source, installed app, live bindings or settings changed. Actual command dispatch, focus, dismissal/chaining, screen-edge placement, unusual key labels and non-Windows behavior remain for implementation verification.
