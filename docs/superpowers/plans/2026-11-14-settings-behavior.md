# Settings Behavior Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for inline implementation. Keep the native checks on a private Windows desktop.

**Goal:** Turn the approved Behavior previews into the desktop Settings page.

**Architecture:** Reuse the existing Settings controls, save path, and Navigator shell for the four tabs. Apply those preferences through the desktop controller with the existing countdown parser, overlay policy, and focus dialog.

**Tech stack:** C#, Avalonia, .NET 10, private desktop regression harness.

**Design:** `design/one-piece-v2/settings/behavior-native-preview/README.md` and its captures.

## Constraints

Use the approved Timer, Idle stop, Focus tracking, and Project categories layouts. Keep wide cards beside each other, stack them in constrained windows, and keep Done reachable. Preserve the settings schema, alternate themes, timer identities, project history, and existing work in the checkout. Use sample state and temporary files for native tests. The current desktop controls define the scope.

## Steps

- [x] Add native checks for stored focus values, selecting a project rule, activating the four tabs, and category validation. Confirm that the old page fails the new expectations.
- [x] Add `SettingsWindow.Behavior.cs` to build the approved cards from the existing named fields. Restore their original locations and styles on a theme change. Route category edits through the existing settings change event and validate numeric fields without committing invalid edits.
- [x] Verify the controller consumes the preferences. Cover a permitted and an exempt idle rule, retention of earlier elapsed time, the explicit overlay-show autostart path, the dedicated show-only shortcut, a smart countdown, and a resume prompt that records the selected reason.
- [x] Run the private native checks at 1120 × 720 and 840 × 600, 70%, 90%, and 125%, including enlarged text and theme changes. Capture all four production tabs plus representative compact and validation states. Run affected Settings regressions and the unit suite.

## Implementation decisions

Auto-start follows explicit overlay toggles. The dedicated show shortcut only shows the clock. Colon blinking applies in clock mode as in the older app. Idle-stop rules apply to stopwatch runs and respect the latest run boundary when removing idle time. Use the current focus dialog for resume classification. Keep legacy WPF feature work outside this page.

## Verification

The native build finished with zero warnings and errors. All ten private desktop check groups and 1,053 unit tests passed. Native checks covered countdown workspace restore and expiry in clock mode, focus prompts for multiple timers, idle-history boundaries, Settings navigation, and controller mode selection. Production page captures with sample settings are in `design/one-piece-v2/settings/behavior-implemented/`.
