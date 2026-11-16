# Main Shortcuts implementation plan

Goal: integrate the approved Main Shortcuts native design without changing command mappings or global registration ownership.
Spec: design/one-piece-v2/shortcuts/main-native-preview/README.md.
Architecture: reuse the existing TextBoxes, slider, validation and draft handlers in a Nika-only partial layout; controller supplies the theme. Shared page appearance continues to scale the window. Legacy layout stays available for other themes.

- [x] Add private-desktop failing checks for the native layout and Escape capture cancellation.
- [x] Add ShortcutsWindow.Navigator.cs, connect the controller, preserve staged save/cancel/reset, expose existing command mappings as the second page, keep validation visible.
- [x] Check capture/clear/reset, modifier-only keys, duplicate globals, command validation, cancel/save, keyboard traversal, legacy themes and 70/90/125% layouts; capture actual production controls.
- [x] Build and run appropriate existing unit/shortcut checks privately. Review completed diff, address material findings and save evidence.

Constraints: no live settings writes, hotkey registrations, installation or Git publication in tests. Preserve approved preview artifacts. Use current checkout. Reset retains its existing all-shortcuts scope and says so. Escape cancels an unmodified capture; Tab/Shift+Tab traverse fields rather than becoming an accidental binding. Command validation must reveal the affected mapping page.

Review focus: capture must not apply canceled edits; hidden mappings must remain editable/validated; all default/global bindings must be preserved; layout must keep actions reachable with scaling and registration errors; shared scale and appearance must not touch the clock overlay.
