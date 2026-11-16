# Keyboard Shortcuts — Main Shortcuts implemented

Production ShortcutsWindow captures at 70%, 90% and 125%, using isolated native windows on private Windows desktops. The app's shared frame renderer and page scaling are reused; the clock overlay artwork is untouched.

Nika uses the approved two-card layout with the four existing main binding fields, command-chaining slider, fixed actions and a Command mappings tab containing the existing mappings UI. Other themes retain their previous layout. Edits remain drafts until Save; Cancel does not accept them. Reset all shortcuts keeps the existing scope and explicitly includes command mappings and chaining timeout.

Main capture ignores modifier-only presses. Unmodified Escape cancels capture; Tab/Shift+Tab are allowed to traverse controls instead of becoming accidental assignments. Modified navigation combinations retain existing capture behavior. Duplicate global bindings block Save. Invalid command mappings reveal the corresponding tab. The example uses the current draft leader instead of a hard-coded Win+F2.

Platform registration status is kept separately from duplicate validation, so newer asynchronous platform messages survive editing or resetting the conflict. This review finding received a failing synthetic regression before the fix.

Evidence workspace: .superpowers/sdd/2026-11-16-shortcuts-main. Native fixtures exercise actual controls and handlers without registering real system hotkeys or saving user settings. Initial layout/Escape and subsequent attached-column/stale-validation failures were reproduced before correction. The full existing unit suite passed 1,117 tests. The existing timer/note shortcut regressions use synthetic hook events and temporary project stores.

Captures main-70.png, main-90.png, main-125.png show the initial page. timing-* show scrolled content when required. recording.png and duplicate.png show production capture and validation states.

No installation or Git publication was performed. Actual OS key capture/registration, physical keyboard traversal, screen-reader output and Linux rendering were not exercised. The existing unit suite contains a network-probe smoke test.
