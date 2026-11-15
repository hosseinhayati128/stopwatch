# Internet Connection & Monitoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved Settings connection/monitoring page with working manual checks and desktop background monitoring.
**Architecture:** Reparent the existing Settings controls into the existing shared Settings shell. Keep the existing export controls accessible in a secondary tab. Reuse InternetSpeedProbe and InternetMonitorService; inject only their I/O boundaries in tests. The desktop controller owns the monitor lifetime, marshals results to Settings, and publishes running-timer state without another polling timer.
**Tech Stack:** Avalonia 11.2.4, C#/.NET 10, existing native private-desktop fixture and xUnit.
**Spec:** design/one-piece-v2/settings/internet-connection-native-preview/README.md (approved native previews).

## Global Constraints
- Native tests run exclusively through Run-HiddenDesktop.ps1. No live network, user settings/history, app deployment, Git publication, or overlay-art changes.
- Reuse the approved frame, colors, 70/90/125% page scaling and fixed Done footer; stack cards in narrow windows.
- Manual testing works with automatic checks off or invalid interval text. Interval is an integer 1–120 minutes; invalid edits retain the saved value.
- Keep historical connection evidence separate from monitoring preference. Timeouts may mean a blocked probe; missing measurements are not zero.
- Preserve legacy themes, History & export access, and existing automatic log behavior. Manual tests remain display-only.

## Review Focus
- A late response after navigation/theme change/close must neither overwrite new state nor unlock a newer check.
- Editing an interval character by character must not restart the service with an intermediate value or erase a previous valid value on invalid input.
- Disabling/restarting/disposal during a scheduled check must invalidate its completion and prevent logging; timer-only mode must not enumerate the UI timer collection from a worker thread.
- Unknown/long/RTL network names, missing speed and large text must not overflow or invent measurements.
- Repeated theme rebuilds must restore original controls exactly once, including export controls and event routes.

### Task 1: Native Settings page and manual actions
Files: create SettingsWindow.Internet.cs and SettingsWindow.InternetActions.cs; modify SettingsWindow.axaml.cs, SettingsWindow.axaml, SettingsWindow.Navigator.cs; create SettingsInternetNativeChecks.cs and add its Program.cs mode.
- [x] Add native checks for real page presence, injected probe, result states, independent preferences, interval validation, cancellation/reentry, theme restoration and 6 window/scale combinations; observe failure on current code.
- [x] Add constructor overload retaining existing five-argument constructor; inject Func<int,CancellationToken,Task<InternetCheckResult>> and optional Func<bool> timer-state provider. Implement RunInternetCheckAsync, CancelInternetCheck, RefreshInternetConnection and UpdateInternetConnection.
- [x] Reparent existing four connection controls; move existing export controls as one named XAML panel into History & export. Add BuildInternetPage/RestoreInternetPage/ResizeInternetPage and navigation hooks.
- [x] Run --settings-internet-native privately; expected all native groups pass with captured production page.

### Task 2: Desktop monitor ownership and safe lifetime
Files: InternetMonitorService.cs; new InternetMonitorServiceTests.cs; new ControllerWindow.Internet.cs and narrow ControllerWindow.axaml.cs hooks.
- [x] Add isolated service tests: enabled/timer-only eligibility, no overlapping checks, single history write/result, restart/disposal invalidation, and manual check with monitoring off; observe failures before implementation.
- [x] Add an internal probe/save constructor to InternetMonitorService; synchronize pending checks and cancel/invalidate them on restart/disposal, preserving first-check 30s and 1–120 minute scheduler.
- [x] Wire controller start/reconfigure/close, cache running state from existing timer tick, and forward check results to Settings on UI dispatcher with stale-service guards. Preview controllers never start the monitor.
- [x] Verify service tests and native controller lifecycle checks privately.

### Task 3: Regression evidence and review
- [x] Build unit/native projects, run the full core suite and focused Settings/native controller regressions privately. Preserve prior design artifacts if fixtures rewrite them.
- [x] Inspect production captures at all requested scales; add implementation README and verification receipt.
- [x] Fresh whole-change review using the executing-plans review gate; resolve important findings with regression coverage.
- [x] Leave source and captures ready for review; installation and Git publication are separate user-requested steps.
