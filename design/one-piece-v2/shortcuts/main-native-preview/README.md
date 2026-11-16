# Keyboard Shortcuts — Main Shortcuts proposal

Native Avalonia preview using the existing shared frame renderer and bundled fonts. Synthetic default bindings only; no global hotkey registration, live settings reads/writes, installed-app update or Git publication.

The proposed two-card layout separates four main bindings from command chaining. At 125% the cards stack in a scrolling content area while the heading and Save/Cancel footer stay fixed. Main/Command mappings labels show the planned two-layer structure; command mappings are outside this design.

Current defaults are read from AppSettings and displayed with Shortcut.Format: Win+F2, Win+F3, Shift+Win+F7 and Shift+Win+F2. Chaining remains 0.2–2.0 seconds, default 0.5, after eligible timer commands. Commands which open panels leave command mode immediately; the timeout is not the initial leader timeout.

The proposal retains staged edits with Save and Cancel. Preview clicks can demonstrate listening, unassigned and reset states; they never record a system shortcut or save settings. Implementation should retain existing combination validation, duplicate detection, registration feedback, platform-specific key names, and reset scope (the existing reset also resets command mappings). The recording screenshot is a visual example, not a verified global input capture flow.

A single long form would mix timing guidance with bindings; this two-card design follows the established Settings layouts. No search, extra shortcut actions or new scheduler is proposed.

Captures: main-70.png, main-90.png, main-125.png. timing-125.png shows the second stacked card. recording.png demonstrates capture feedback. All preview windows are 1120x720 on a private Windows desktop. Build and native layout/scroll/focus checks are recorded in .superpowers/sdd/2026-11-16-shortcuts-main-preview.

Design only, awaiting review. Production integration, actual capture/save/cancel/reset behavior, registration conflicts and Linux rendering are not tested in this preview.
