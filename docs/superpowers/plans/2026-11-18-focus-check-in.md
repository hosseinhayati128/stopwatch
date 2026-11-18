# Focus Check-in Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline. Steps use checkbox syntax for tracking.

**Goal:** Apply the approved compact Focus Check-in design to the production dialog while retaining its classification and completion contract.

**Architecture:** Reuse the existing four named buttons and countdown, adding a Nika appearance partial. Pass the current settings from the controller through an optional final constructor/ShowAsync argument. Derive the visual default and submitted default from one expression; keep history recording and task-switch detection unchanged.

**Tech Stack:** C#, Avalonia, existing shared frame renderer and private Windows desktop harness.

**Spec:** `design/one-piece-v2/focus-check-in/native-preview/README.md` and its approved captures.

## Global Constraints

- Native execution in the existing checkout; preserve unrelated logo work.
- Tests and screenshots only on a private desktop, using synthetic values.
- Four choices retain order; 1–4/numpad choose, S selects Task switch, Enter/Space accept the automatic choice, Escape/close skip.
- Nika logical size 520 × 538 normal, 520 × 570 task switch, at 70/90/125% page scale. Reuse current ornaments; no new artwork/dependencies.
- Existing thresholds/timeouts remain configurable. Midnight/Daylight keep their current layout.
- No new task-switch detection, live-data writes, installation, commit or push in this task.

## Review Focus

- Nondefault button focus must not override Enter/Space's advertised default.
- Threshold boundary, custom threshold and missing switch details must agree between highlight, countdown and callback.
- Long/RTL names and enlarged text must keep close/footer/actions reachable; overflow can scroll.
- Timer expiry and every dismissal path must invoke the result once and stop the timer.
- Current settings must reach the queued controller prompt; recording must remain tied to its timer.

### Task 1: Production popup and verification

**Files:** Create `FocusInterruptionDialogWindow.Appearance.cs` under Desktop/Views and `FocusCheckInNativeChecks.cs` under tools/DesktopRegressionChecks. Modify the existing dialog code-behind, ControllerWindow.Behavior.cs, DesktopPageAppearance.cs and the regression Program.cs. Preserve the existing XAML and named handlers.

**Interfaces:** Append `AppSettings? settings = null` to the dialog constructor and `ShowAsync`; add `internal void FitFocusContent()`. Reuse `DefaultReason` internally for display and expiry. Add native mode `--focus-check-in-native`; use existing queued-controller focus check in the real app fixture.

- [x] Add native layout/action tests and watch the missing shared-frame/countdown contract fail on the current dialog.
- [x] Implement approved appearance; use a scrollable summary/choice body with fixed heading/footer for long names and larger fonts. Reuse existing buttons and decision methods, with explicit close-to-skip.
- [x] Pass settings from controller; fit frame after opening/resizing/page-scale changes. Exercise normal and supplied switch state at 70/90/125%, 125% text, RTL and long names.
- [x] Verify button actions, keys including focused buttons, default threshold boundaries, custom countdown, repeated close/expiry callback count and timer shutdown; retain classic themes.
- [x] Build with `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true`. Run new mode via `Run-HiddenDesktop.ps1`; expect no failures. Run the existing controller focus-queue/history check on a private desktop.
- [x] Inspect production captures, get a final independent review, address material issues, save evidence/hashes and show results. Leave changes uncommitted for the user's separate publication request.
