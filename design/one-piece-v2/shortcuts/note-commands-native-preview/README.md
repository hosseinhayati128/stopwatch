# Note Commands — native design proposal

Compact Win+F3 popup matching Timer Commands: parchment, navy text, gold key badges, two rows and a small leader badge. Logical size: 620 × 132. No expanded stage is proposed because all six controls fit.

Reading order: T Add Todo, N Quick note, R Add reminder; V View notes, O Show/hide todos, Space Expand/collapse. The footer clarifies that Space affects the Todo panel. Escape cancels. Default mappings come from AppSettings; implementation must follow custom mappings and omit the Space fallback when Space is assigned to another note command.

Native Avalonia previews: note-commands-70.png, note-commands-90.png and note-commands-125.png. These are an isolated presentation prototype, with no hooks, live settings or command dispatch. The running app is unchanged.

Verification: fixture-only build and native layout checks for all three scales; six controls, text bounds, footer separation and native window dimensions checked. Rendered entirely through Run-HiddenDesktop.ps1 on a private desktop, without switching desktops. Images visually inspected. Logs: .superpowers/sdd/2026-11-16-note-commands-preview/.

Design only: no production implementation, installation or Git publication. Full shortcut tests are reserved for integration.
