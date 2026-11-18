# Close Stopwatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement inline. Steps use checkbox syntax for tracking.

**Goal:** Integrate the approved compact close dialog while preserving tray, exit and cancel behavior.

**Architecture:** Add a Nika appearance partial using the existing named controls/handlers and shared frame. Pass current settings from the controller. Keep the existing close-action preference flow, guard duplicate prompts, and enforce the advertised Enter/Esc contract before focused controls handle keys.

**Tech Stack:** C#, Avalonia, shared page scale/typography, private Windows desktop native checks.

**Spec:** `design/one-piece-v2/close-stopwatch/native-preview/README.md` and its approved captures.

## Global Constraints

- Native execution in the current checkout. Preserve pending Activity Review, prototype and logo work.
- All native windows/tests on a private desktop with synthetic settings; never save real preferences or shut down the installed app.
- Nika logical size 500 × 428, verified at 70%, 90%, 125%; other themes retain their layout.
- Enter minimizes; Esc, Cancel and close dismiss without choosing Exit or saving a preference. Explicit Exit remains available by button activation.
- Remember the action only after a non-cancel result. No new setting, asset, dependency or timer.
- No installation, commit or push until separately requested.

## Review Focus

- Enter from focused Exit/Cancel/checkbox must follow the displayed minimize shortcut; Space can activate the focused control normally.
- Modal and ownerless results complete once; closing the owner or repeating a close request must not leave orphan dialogs or stale callbacks.
- Cancel with Remember checked never changes the saved action; preview controllers never write user settings.
- Large fonts/live zoom/small working areas keep actions, preference and fixed footer reachable.
- Native owner centering and screen bounds respect Windows scaling; primary hover/pressed states remain readable.

### Task 1: Appearance, behavior and integration

**Files:** Create `CloseActionDialogWindow.Appearance.cs` and `CloseStopwatchNativeChecks.cs`. Modify close dialog XAML/code-behind, controller close handler, `DesktopPageAppearance.cs`, regression `Program.cs`, and the legacy close-theme fixture's explicit theme input.

**Interfaces:** Append optional `AppSettings? settings = null` to the string-theme constructor and ShowAsync. Add `FitCloseDialogContent(bool resizeWindow = false)` and a work-area placement helper, following the reviewed Activity Review implementation. Add native mode `--close-stopwatch-native`.

- [x] Add native layout, focused-key, result/persistence and duplicate-controller-close tests; run against the current code and confirm intended failures.
- [x] Implement the approved layout, dynamic Remember hint, vector controls and fixed header/footer with scrollable body. Reuse named buttons and checkbox; name the existing Cancel button.
- [x] Enforce Enter/Esc routing and idempotent result selection. Pass settings and guard the controller's pending close request; retain existing result handling and suppress settings writes for the existing preview mode.
- [x] Build the native fixture with `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true`; run `--close-stopwatch-native` only through `Run-HiddenDesktop.ps1`. Expect layout, action/modal and controller groups to pass.
- [x] Inspect real captures, obtain one fresh independent review, address material findings and save evidence. Leave the app and Git publication unchanged.
