# Periodic Activity Review Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline. Steps use checkbox syntax for tracking.

**Goal:** Apply the approved compact design to the real Activity Review reminder without changing scheduling or review outcomes.

**Architecture:** Reuse the existing named buttons, countdown and result handlers through a Nika appearance partial. Pass current settings from the controller using an optional final constructor/ShowAsync argument. Keep the shared scheduler and review opening paths intact.

**Tech Stack:** C#, Avalonia, shared frame/typography/page scale and private Windows desktop harness.

**Spec:** `design/one-piece-v2/periodic-review-prompt/native-preview/README.md` and approved native captures.

## Global Constraints

- Native implementation in the current checkout; preserve unrelated logo work and approved prototype files.
- All verification windows stay on a private desktop. Use synthetic periods/settings; no real history or external requests.
- Nika logical size 480 × 396, checked at 70%, 90%, 125%; retain other themes' existing layout.
- Y/Review now means Accepted; Esc/Skip means Skipped and the regular interval; close/timeout means Ignored and snooze. No new automatic action or shortcut.
- No installation, Git commit or push until separately requested.

## Review Focus

- Focused controls must retain Y/Esc; callbacks run once after closure and timers stop on every dismissal path.
- Custom intervals must show the correct skip and snooze times independently; timeout must never accept a review.
- UTC duration remains accurate across local midnight/DST; date labels distinguish today, yesterday and older dates.
- Enlarged typography, live page scale and small work areas keep actions and footer reachable.
- Controller prompts receive current settings; existing suppression, cancellation and scheduler lifecycle remain intact.

### Task 1: Production appearance and verification

**Files:** Create `StopwatchOverlay.Desktop/Views/PeriodicReviewPromptWindow.Appearance.cs` and `tools/DesktopRegressionChecks/PeriodicReviewPromptNativeChecks.cs`. Modify dialog code-behind, `ControllerWindow.PeriodicReview.cs`, `DesktopPageAppearance.cs`, regression `Program.cs` and the existing controller lifecycle check in `SettingsActivityWatchReviewNativeChecks.cs`.

**Interfaces:** Append `AppSettings? settings = null` to constructor and ShowAsync; add `internal void FitReviewPromptContent(bool resizeWindow = false)` and screen constraint helper following Focus Check-in. Page-scale changes request a window resize; native size events only refit content. Add native mode `--periodic-review-prompt-native` and reuse existing controller lifecycle checks.

- [x] Write native layout/action checks and run the missing-frame/countdown contract against the current popup; expect failure for the unimplemented design.
- [x] Apply the approved frame/header/period/buttons/footer using existing controls and handlers. Keep header/footer fixed and allow body scrolling when necessary. Add date-aware local display and duration from UTC endpoints.
- [x] Pass settings from controller; refit after opening/resizing/page-scale changes and constrain to the monitor work area.
- [x] Build with `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true`. Run new mode through `Run-HiddenDesktop.ps1`; expect all layout, period, lifecycle and controller groups to pass.
- [x] Inspect real-window captures at all three sizes plus custom timing, overnight, larger text and small-screen states. Obtain a fresh final review, address material findings and save evidence. Leave installation/publication for the user's next request.
