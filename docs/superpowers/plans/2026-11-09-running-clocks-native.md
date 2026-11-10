# Running Clocks Native Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. The user has approved native implementation; continue without another design approval gate.

**Goal:** Replace Nika's basic Running Clocks shelf with the approved, roomier native layout.

**Architecture:** Keep the controller's existing callbacks, formatter and refresh cadence. Reuse StopwatchFrameSurface and Navigator typography/colors, with real Avalonia Buttons and ScrollViewer. Retain the plain shelf for other application themes.

**Tech Stack:** C#, Avalonia, existing private-desktop regression harness.

**Spec:** `design/one-piece-v2/running-clocks/native-layout-preview/README.md`; authoritative image is `running-clocks-layout.png`, not the denser state study.

## Global Constraints

- Width 350 DIP; rows 44 DIP; list viewport up to 220 DIP. Outer heights: three rows 292, five visible rows 380, empty 194.
- Engraved Filigree, artwork scale .14, padding 7, parchment background; no new artwork, fonts, dependencies, settings or refresh timer.
- Selection activates only; Pause pauses only its timer; Hide dismisses only the shelf. Exclude wall clocks and paused timers. Keep current time formatting.
- Native controls, keyboard focus, accessible names, full times and trimmed long names; Persian names retain RTL flow.
- All native windows and input checks stay on the private desktop. Preserve unrelated local changes; no Git publication.

## Review Focus

- Removing a focused row must not move focus onto another Pause action.
- Repeated updates must retain controls and avoid rebuilding unchanged layout.
- Theme changes and hide/reopen must not retain stale content or preferences.
- Initial show and changed row count must keep the shelf within the anchor monitor.
- Long times and names, scrolling, keyboard activation and transparency must work in the real controls.

### Task 1: Integrate and verify the native shelf

**Files:** `StopwatchOverlay.Desktop/Views/ActiveClocksShelfWindow.cs`, `ControllerWindow.Shortcuts.cs`, `tools/DesktopRegressionChecks/RunningClocksChecks.cs`, `Program.cs`, and native capture documentation under `design/one-piece-v2/running-clocks/`.

**Interfaces:** Keep the two callback constructor and `UpdateClocks(IReadOnlyList<TimerSession>, TimerSession?, Func<TimerSession,string>)`. Add `ApplyTheme(string)` consumed by the controller before update. Keep `RepositionUnder(OverlayWindow)` and reapply placement after show/size changes.

- [x] Add real-window regression checks for shared-frame layout, selection/pause/hide and focus, long times/RTL, scrolling, stable updates, themes, and monitor-edge placement. Add `--running-clocks-only` routing.
- [x] Build and run that mode using `Run-HiddenDesktop.ps1`; expect failures for the missing framed layout and focus behavior. Keep baseline output.
- [x] Implement native controls with the existing style/template pattern. Rebuild rows only on membership/order/theme changes. Reposition on opening and size changes.
- [x] Build and rerun the focused mode; expect all checks to pass. Capture three/many/empty actual controls and inspect against the approved layout.
- [x] Run existing shortcut regression and the full unit suite on the private desktop; report any failure. Obtain one fresh independent review, fix material findings with a reproducing check.
- [x] Publish a separate Release candidate, and update the saved executable only if the app is closed. Preserve a rollback build, leave the app closed, and verify hashes. Leave Git changes uncommitted for review.

Self-review: one native UI deliverable, no additional settings or application model changes. Theme isolation and positioning are included in its checks.
