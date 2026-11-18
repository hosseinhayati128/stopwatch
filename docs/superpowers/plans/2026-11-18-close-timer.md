# Close Timer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement inline. Steps use checkbox syntax for tracking.

**Goal:** Integrate the approved Close Timer confirmation without changing unrelated confirmation dialogs.

**Architecture:** Opt in the existing ConfirmationDialogWindow through ConfigureCloseTimer(TimerSession session, string displayTime, AppSettings settings). Reuse its controls/results and the controller's existing close lifecycle. Add a CloseTimer appearance partial and shared-page fit hook; no shared-frame or overlay artwork changes.

**Tech Stack:** C#, Avalonia, existing frame/typography, private Windows desktop native fixture.

**Spec:** design/one-piece-v2/close-timer/native-preview/README.md

## Global Constraints

- Compact Nika logical size 500 × 350; shared frame, navy Cancel, muted-red Close Timer.
- Native checks at 70%, 90%, 125% stay on a private desktop with synthetic settings and timers.
- Capture timer name, mode/state and caller-formatted time at opening; no new ticker. Clock caption is Current time; countdown is Remaining; stopwatch/timecode use Elapsed.
- Initial focus on Cancel; Escape/X cancel; Enter/Space activate the focused action, with no unfocused Enter-to-close fallback.
- Retain legacy themes and all non-Close-Timer confirmation behavior, including Todo-specific appearance.
- Preserve pending Activity Review, logo and approved preview work. No install, restart, commit or push in this request.

## Review Focus

- A running/paused, clock, countdown or timecode timer must display the correct identity and time meaning; long/RTL names and long durations stay readable.
- Cancel and focus loss cannot close a timer; explicit confirmation completes once. Repeated or stale callbacks must not remove a different timer.
- Controller-visible modal and hidden-controller ownerless paths must release the existing dialog guard; only the intended timer/overlay is removed, while other timers and saved history survive.
- Page zoom, larger text and constrained working areas keep the summary reachable and both action buttons visible; centered placement respects owner and Windows scaling.
- Generic confirmation and Todo appearance callers must retain their current behavior and must not read/write user preferences during native checks.

### Task 1: Close Timer integration

**Files:** Create ConfirmationDialogWindow.CloseTimer.cs and CloseTimerNativeChecks.cs. Modify ConfirmationDialogWindow.axaml.cs, ControllerWindow.axaml.cs, DesktopPageAppearance.cs and regression Program.cs. Keep XAML controls and handlers.

**Interfaces:** Internal ConfigureCloseTimer(TimerSession session, string displayTime, AppSettings settings); FitCloseTimerContent(bool resizeWindow = false); FitCloseTimerToScreen(Screen screen); native mode --close-timer-native.

- [x] Add native layout/mode/focus/action/controller checks and run them against existing behavior. Expect missing frame, unsafe unfocused Enter and missing controller summary failures.
- [x] Implement the approved opt-in layout, snapshot summary, safe focus/key behavior and idempotent completion. Call from CloseSession with current settings and FormatDisplayTime(session), preserving existing timer removal.
- [x] Build with dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:UseSharedDesktopRuntime=true. Run --close-timer-native through Run-HiddenDesktop.ps1; all focused groups must pass.
- [x] Inspect real production captures, obtain one fresh independent review, address material findings and record evidence and remaining limits.
