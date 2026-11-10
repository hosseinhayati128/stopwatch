# New Timer / Choose Project Native Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. Native previews and implementation are approved; continue without another design gate.

**Goal:** Integrate the approved New Timer / Choose Project design while preserving result and cancellation semantics.
**Architecture:** Reuse TimerNameWindow's controls/handlers in a Nika-only layout partial. Share the existing native form styles with Timer Editor; controller supplies theme explicitly. Other themes retain their layout. No new settings, assets, dependencies or refresh loop.
**Tech Stack:** Avalonia/C#, existing private-desktop regression harness.
**Spec:** `design/one-piece-v2/project-chooser/native-layout-preview/README.md` and its three approved PNGs.

## Global Constraints

- 560 DIP width; 490 DIP normal height, 602 DIP with new project. Shared Engraved Filigree at .16, padding 10, parchment. Header/footer fixed; constrain height to monitor and scroll when needed.
- New Timer uses Create timer; current timer uses Apply project. No project remains an unnamed timer. New/Back labels replace plus/close in Nika; preserve category/score and new-category entry.
- Keep project/category limits, Enter acceptance, Escape returning from drafts before closing, callback-loaded project defaults and cancellation without writes.
- All native tests on the private desktop. Preserve unrelated Todo artwork and approved preview assets. No Git publication requested.

## Review Focus

- Selecting an existing project while a new-project draft is open must load that project's category/score, not retain stale draft defaults.
- Invalid names/category and numeric extremes must not crash, silently accept invalid draft values, or close on a popup selection key.
- Both expanded editors, long/RTL names and short monitors must retain editable fields, readable errors, focus and visible footer.
- Theme selection must reach both controller shortcuts and optional helper calls; plain themes and Timer Editor must remain compatible.
- Cancellation, repeated open/close and queued focus actions must not write live settings/history or retain a refresh loop.

### Task 1: Integrate the chooser and verify delivery

**Files:** `TimerNameWindow.axaml`, `.axaml.cs`, new `.Navigator.cs`; `ControllerWindow.Shortcuts.cs`; shared `NavigatorFormStyles.cs` and existing Timer Editor style caller; `ProjectChooserNativeChecks.cs`, harness `Program.cs`, native capture README.
**Interfaces:** Preserve existing constructor/ShowAsync arguments and result properties; append optional theme string. Retain CreateProjectChooser/ApplyProjectChoice contracts. Share existing button/input style rules without changing their behavior.

- [x] Add focused tests for frame/layout, default/create/apply/unnamed outcomes, validation, new project/category and Escape, callbacks and extreme scores, popup/keyboard/RTL, other themes and actual captures. Build and run `--project-chooser-native` privately; expect missing-frame and demonstrated behavior failures.
- [x] Integrate native layout and scoped styles with the existing controls; fix reproduced input issues at their shared handlers. Focused checks must pass, including short-height scrolling and footer visibility; inspect actual captures against previews.
- [x] Run Timer Editor style regressions, shortcut regression and full unit suite privately. Obtain one independent final review and resolve material findings with regression evidence.
- [x] Publish a separate Release candidate; install only while app closed and prior build hash matches, retaining verified rollback. Leave app closed. Document results/limits; no commit or push.
