# Settings Fonts & Colors Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task.

**Goal:** Integrate the approved native Fonts & colors design and make the existing typography preferences work in the Desktop application.

**Architecture:** Keep the existing settings schema and editor callbacks. A Desktop typography service resolves the six scopes, preserves underlying font/color sources, and updates only on settings, theme, or control lifecycle changes. The Settings partial reuses existing editors and restores them for other themes. Painted clocks receive explicit scale/color inputs.

**Spec:** `design/one-piece-v2/settings/typography-native-preview/README.md` and the user's approval to continue.

**Constraints:** Reuse current feature checkout; preserve unrelated Todo PNGs. No installation, restart, commit or push in this task. All native checks run through Run-HiddenDesktop.ps1. No polling, new artwork, dependencies, or settings migration.

**Review focus:** Underlying bindings must survive scale/reset; inherited and input text must not scale twice; selection/button text stays readable; popup scope follows its owner; theme switches and closing Settings during a slider gesture release pending work.

## Task 1: Typography application

Files: new `StopwatchOverlay.Desktop/DesktopTypography.cs`; `App.axaml.cs`; `Views/ControllerWindow.axaml.cs`; `Controls/ThemedClockFace.cs`; `Controls/StopwatchAppearanceRenderer.cs`; `Controls/PirateArtwork.cs`; new `tools/DesktopRegressionChecks/SettingsTypographyNativeChecks.cs`; harness `Program.cs`.

Interface: `Initialize(TypographySettings)`, `Apply(TypographySettings)`, `Shutdown()`, `SetScope(Control,string)`, `SetPreserveColor(Control,bool)`, `Refresh(Control)`; settings-normalized scope resolution. Custom controls expose typography scale/brush without replacing theme defaults.

- Write and run failing native checks for scaling bound/local/inherited text, dynamic controls, six scopes, popup ownership, theme/default restoration, selected text, and special clocks.
- Add the event-driven service and lifecycle/settings hooks. During slider gestures, preview immediately but defer application-wide work until release.
- Rerun native checks; require no compounding, no lost bindings, no lingering window references after closure.

## Task 2: Approved Settings panel

Files: new `Views/SettingsWindow.Typography.cs`; `Views/SettingsWindow.Navigator.cs`; `Views/SettingsWindow.axaml.cs`; `Controls/TypographyEditor.cs`.

Interface: build/restore typography page; rebuild editor hosts after settings reload; resize based on available logical width; live specimen with explicit scope and readable selection colors.

- Add failing checks for two tabs, six panel options, native hex validation, scope inheritance, and theme restoration.
- Integrate the approved layout using the existing editor instances and saved callbacks. Preserve legacy theme behavior and footer.
- Verify text sizes 75/100/175%, page scales 70/90/125%, wide/compact windows, disabled overrides, keyboard entry, and closing during changes. Capture production UI.

## Task 3: Verification and handoff

- Build Desktop and the native harness with zero errors, run focused typography, Settings shell/Appearance, controller-sizing and overlay performance regressions privately.
- Run existing unit tests privately if shared behavior changes warrant it.
- Obtain one fresh final review, resolve material findings with regression coverage, inspect screenshots, and show the production result in chat.
- Record remaining limitations honestly. Leave installed app and Git publication unchanged.

## Completion

All three tasks are complete. Desktop and native harness builds passed with zero warnings/errors; 9 typography groups and 7 regression groups passed on private desktops, including 30 controller-sizing states. Production captures and verification scope are documented in `design/one-piece-v2/settings/typography-implemented/README.md`. One final review completed; its preview-scope and custom-color selection findings were fixed with checks. The installed application and Git publication were left unchanged at implementation handoff.

The user subsequently requested installation and Git publication. The release was installed and startup verified. Publication verification also passed all 1,052 unit tests; the archived proposal fixture was adapted to retrieve its editors from the integrated page and passed its native check.
