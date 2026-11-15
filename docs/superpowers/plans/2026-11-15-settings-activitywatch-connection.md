# ActivityWatch Connection & Status Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Implement the approved ActivityWatch Connection & Status layer in Settings and show native production captures.

**Architecture:** Add SettingsWindow partials for layout and guarded connection actions, reparenting the existing controls and retaining their settings callbacks. Reuse ActivityWatchClient and ActivityWatchLauncher; add optional HTTP/launcher injection for isolated native checks. Keep the existing export and review controls available under their tabs without redesigning them.

**Tech Stack:** C#, Avalonia, shared Settings frame and private-desktop native regression harness.

**Spec:** design/one-piece-v2/settings/activitywatch-connection-native-preview/README.md, approved by the user's implementation request.

## Global Constraints

- All tests and captures run off-screen on a private desktop with fake HTTP and a fake launcher. Do not install, restart, commit or push in this implementation request.
- Preserve the shared shell, clock artwork, other Settings pages, existing export/review actions and legacy themes. No dependencies.
- Integration enabled and server reachability are independent. Initial state is untested; editing the effective address invalidates prior results.
- Only HTTP/HTTPS addresses enable Test; blank/bare-host addresses retain existing defaults. Local Start is limited to loopback addresses.
- No background connection polling. Pending actions disable Test/Start/address and have bounded lifetimes; navigation, address changes and closing cancel stale feedback.
- Approved previews are retained. Production tabs keep the existing Sync & export / Review prompts forms accessible instead of shipping the preview's disabled placeholders.

## Review Focus

- Late responses after address changes, navigation, theme replacement or close must not overwrite a newer result or leave actions disabled.
- Launch failure, server failure, cancellation and a successful launch without a server response require accurate, distinct feedback.
- Reparenting/restoring controls must preserve settings values, callbacks, original theme styles and the existing export/review functionality.
- Invalid or remote URLs must not launch a local process; enablement changes must not report a successful connection.
- Compact windows and enlarged typography must retain readable actions, scrolling and the fixed Done button.

### Task 1: Implement and verify the Connection & Status layer

**Files:** Add SettingsWindow.ActivityWatch.cs and SettingsWindow.ActivityWatchActions.cs; modify SettingsWindow.axaml, .axaml.cs and .Navigator.cs; add optional HTTP injection to Core/ActivityWatch/ActivityWatchClient.cs. Add tools/DesktopRegressionChecks/SettingsActivityWatchNativeChecks.cs and its Program.cs dispatch. Add implemented PNGs and README under design/one-piece-v2/settings/activitywatch-connection-implemented.

**Interfaces:** Preserve existing public SettingsWindow constructors and existing ActivityWatchClient callers. Add an internal SettingsWindow constructor taking optional ActivityWatch HTTP client/launcher seams for tests. Reuse TestConnectionAsync(CancellationToken), TryLaunch(), CommitControls(SettingsChangeKind.ActivityWatch), and the existing export/review handlers.

- [x] Add focused native checks for production page controls, persisted callbacks, retained tabs, real client responses through fake HTTP, busy/late/closed results, launch outcomes, restoration, responsive layout and captures.
- [x] Build and run --settings-activitywatch-native privately; expect missing production page/transport failures before implementation.
- [x] Implement the smallest shared layout/action integration with cancellation and bounded startup checks; preserve old controls and handlers.
- [x] Build and run native ActivityWatch checks, adjacent Settings/Habitica checks and the unit suite privately; expect zero failures. Adapt the archived preview if its replaced host needs restoring.
- [x] Inspect production captures and obtain one fresh final review; address material findings with focused checks and record results. Leave changes uncommitted for the user's normal update/push workflow.
