# Command Mappings implementation plan

Goal: integrate the approved Command Mappings design into the Keyboard Shortcuts window.
Spec: design/one-piece-v2/shortcuts/mappings-native-preview/README.md.
Approach: native execution in the current checkout, preserving the approved preview and existing user work. Reuse real mapping editors, Clear handlers, draft dictionaries, validation and shared page scaling. No live installation, settings writes, hotkey registration, or Git publication.

## Tasks

- [x] Add private-desktop checks that fail for missing production mappings layout and Tab capture; cover both families, custom/cleared leaders, draft isolation, validation routing, Save/Cancel/reset and scale/scroll behavior.
- [x] Add ShortcutsWindow.CommandMappings.cs with the approved groups and responsive columns; call it from ApplyNavigatorLayout. Keep legacy themes unchanged. Connect selected family, validation visibility and accurate draft examples through existing handlers.
- [x] Adapt existing Main Shortcuts checks and archived proposal fixture to the new layout without replacing their intent; build and run native mappings, main shortcuts, existing shortcut regressions and the unit suite privately.
- [x] Review the scoped change, resolve material findings, capture actual production views at 70/90/125%, and record evidence.

## Interfaces and scope

- ShortcutsWindow.Navigator.cs: host the new command content, toggle between main/mapping pages, retain fixed footer and outer shell.
- ShortcutsWindow.CommandMappings.cs: layout, family selection, responsive group placement, draft-aware guidance, accessibility names/tooltips.
- ShortcutsWindow.axaml.cs: preserve current action maps and validation; allow plain Tab/Shift+Tab traversal; refresh examples on changes; reveal the correct family on Save validation failure.
- ControllerWindow already supplies the theme and persists accepted results; inspect its caller, no new persistence path.
- DesktopPageAppearance and ShortcutsWindow.Navigator: constrain and re-center this window's frame after resize or page-scale changes. A minimum-window test and image inspection exposed retained desired size/offset; reuse the existing controller fitting pattern without changing overlay sizing.
- Native fixtures: add --shortcuts-mappings-native; preserve --shortcuts-main-native and --shortcuts-mappings-preview. Use Run-HiddenDesktop.ps1 for every GUI/test run.

## Verification

Run fixture build (no restore, shared runtime), observe successful exit, then the private desktop mode. Expected initial failure: mapping group absent and Tab incorrectly mutates a command. Final build must succeed; native capture/validation/navigation/legacy checks and unit suite must pass. Inspect the rendered PNGs. Confirm installed EXE hash remains unchanged and git diff --check passes.

Review focus: moved controls retain their handlers and values; warnings belong to the active family and reveal invalid drafts on Save; custom and unassigned leader/key examples remain honest; reset has its original all-shortcuts scope; Tab does not become an accidental binding; scroll/footer remain usable at scale; no live app or data touched.

Completed evidence: design/one-piece-v2/shortcuts/mappings-implemented/verification.json and README.md. Native and unit builds: zero warnings/errors. Full unit suite: 1,117 passed, zero failed/skipped. Native mappings, Main Shortcuts, existing routing and archived-preview checks passed on a private desktop. Independent review found no actionable production issues; actual Tab/Shift+Tab focus assertions close its noted coverage limitation. Linux rendering, live installation and Git publication remain outside this request.
