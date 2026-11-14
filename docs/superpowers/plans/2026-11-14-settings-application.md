# Settings Application Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for this integration and its final review.

**Goal:** Integrate the approved Application page into the native desktop Settings shell.

**Architecture:** Reuse the Startup checkbox, existing close-action selection and SettingsChanged save path. Add a SettingsWindow partial for the two cards and responsive layout. Restore the original checkbox when leaving the Nika theme.

**Tech Stack:** C#, Avalonia, .NET 10, private Windows desktop fixture.

**Spec:** `design/one-piece-v2/settings/application-native-preview/README.md` and its native captures.

## Constraints

Keep tests on a private Windows desktop and share implemented-page captures. Retain earlier work in this checkout. Use synthetic preferences for verification.

## Steps

- [x] Add a focused native check that detects the missing Application layout, verifies settings callbacks, restored preferences and alternate-theme recovery.
- [x] Add `SettingsWindow.Application.cs`, route Application navigation to it, and synchronize the radio choices with `CloseActionCombo`. Reuse the existing controller startup and close handlers.
- [x] Verify wide and compact layouts at 70%, 90% and 125%, enlarged text, keyboard access and the fixed footer. Save production captures under `design/one-piece-v2/settings/application-implemented/`.
- [x] Run affected Settings regressions and review the final change. Verify the archived design fixture still renders after integration.

## Review focus

Loading or theme changes must not save preferences. A radio selection must emit a single settings change. Returning from another page or theme must retain the selected action. Startup service readback must be reflected by ReloadFromSettings. Long text and page scaling must leave controls reachable by scrolling.

## Verification

Build completed with zero warnings and errors. Five native groups passed on a private Windows desktop, covering Application behavior and layout plus Settings navigation, persistence feedback, theme recovery and Behavior controls. The archived design fixture passed all six size/scale configurations and regenerated pixel-identical approved captures. The final read-only review found no important issues.

Evidence is in `.superpowers/sdd/2026-11-14-settings-application/`. The installed app was preserved. Tests used synthetic settings and exercised the existing settings callbacks. Windows sign-in startup and real process shutdown were outside these UI checks.
