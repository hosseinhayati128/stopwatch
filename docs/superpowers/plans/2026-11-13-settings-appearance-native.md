# Settings Appearance Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for inline implementation and one final read-only review.

**Goal:** Implement the reviewed Appearance page, including real application scale/background behavior and independent floating-clock controls.

**Architecture:** Reparent existing settings controls into two Nika sections, retaining their current commit/import/validation handlers. Reuse the shared frame and actual clock/controller renderers. Add one desktop presentation service for application zoom and cached background patterns; floating overlays remain independent.

**Tech Stack:** C#, Avalonia 11.2.4, existing Core catalogs, private-desktop regression harness.

**Spec:** `design/one-piece-v2/settings/appearance-native-preview/README.md` and its native captures.

## Global Constraints

- Reuse the current codex/stopwatch-overlay checkout and preserve earlier Settings work plus the six unrelated Todo images.
- Keep tests/previews on a private desktop. No installation, restart, commit or push.
- Keep all catalogs, immediate settings application, persistence feedback, validated image import/removal, 70–125% page-scale range/default 90%, and background strength 10–45%.
- No new artwork or dependencies. Application styling must not alter clock/Todo overlay geometry, saved positions or timer state.
- Preview rendering must not show helper windows, load user history, start services, dispose live hotkeys, or persist sample data.

## Review Focus

- Nika/legacy theme changes restore controls, bindings, focus, and original shell without duplicate parents or handlers.
- Scale changes during mouse/keyboard adjustment leave controls reachable; fixed controller canvas remains scrollable when zoomed.
- Background resources remain bounded, recover from missing/corrupt custom files, and keep imports/removal transactional.
- Preview windows remain hidden and isolated from live controller/platform lifecycle; closed settings release snapshot resources.
- Existing overlays, compact dialogs, new windows and theme transitions retain their independent appearance and positions.

## Task 1: Appearance editor and preview

Files: SettingsWindow.Appearance.cs (new), SettingsWindow.Navigator.cs, SettingsWindow.axaml/.cs, ControllerWindow preview constructor/lifecycle, SettingsAppearanceNativeChecks.cs (new), regression Program.cs.

Interfaces: existing named controls, SettingsChanged, SettingsChangeKind, ReportPersistenceResult, category keys and the current one-shot preview timer.

- [x] Write native checks for two sections, retained options, independent clock editing, no load writes, compact layout, footer, category link and theme restoration. Run privately; expect missing Appearance tabs/preview to fail.
- [x] Build the approved layout with the existing controls and local preview model. Use hidden/unshown synthetic controller rendering, with an isolated close path; never open a helper on the user's desktop.
- [x] Run focused checks and capture the production wide/compact/application/clock states. Expect all checks to pass.

## Task 2: Application presentation consumers

Files: DesktopPageAppearance.cs (new), App.axaml.cs, ThemeResources.cs, StopwatchFrameSurface.cs, ControllerWindow.axaml/.cs and SettingsWindow theme transitions.

Interfaces: AppUiScale/Background catalogs and existing SettingsInteractionStarted/Completed events. Task 1 consumes the resulting application preview and failure status.

- [x] Add failing native checks for 70/90/125% zoom, new-window application, theme/content replacement, overlay exclusion, cached built-in patterns, reset/default and missing custom backgrounds.
- [x] Apply zoom through Avalonia layout transforms. Keep the controller's existing fixed canvas scrollable when zoomed; avoid scaling native floating overlays. Defer scale changes until slider adjustment completes.
- [x] Decode validated background choices once per selection, reuse the image for strength changes, restore theme defaults, and show failures without discarding saved custom choices. Paint patterns beneath controls and inside shared panel frames.
- [x] Verify focused checks, existing Settings/inspector/controller/frame regressions and the full unit suite privately. Expect all pass; record native captures and preservation hashes.
- [x] Fresh read-only final reviewer, then one RED/GREEN fix pass for important findings. Leave all changes uncommitted for review.
