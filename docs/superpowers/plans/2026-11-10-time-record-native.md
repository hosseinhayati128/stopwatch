# Add / Edit Time Record Native Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. The native layout and implementation are approved; execute inline with one final independent review.

**Goal:** Integrate the approved time-record panel without changing historical record semantics.
**Architecture:** Reparent the existing ProjectRecordEditorWindow fields into a Nika-only layout partial, reuse NavigatorFormStyles and StopwatchFrameSurface, and pass theme from all callers. Keep validation and persistence in the existing handler.
**Tech Stack:** Avalonia/C#, private-desktop regression harness.
**Spec:** `design/one-piece-v2/time-record/native-layout-preview/README.md` and approved PNGs.

## Global Constraints

- Width 560 DIP, heights 620/732, shared Engraved Filigree at .16 with padding 10; fixed header/actions and scrollable form. Clamp expanded form to the monitor work area.
- Add/Edit, new project, overnight records, HH:mm/HH:mm:ss local entry, duration and inline errors retain their meanings. No category/score features, new assets, dependencies or refresh timer.
- Preserve 200-character printable names, future/range/DST validation, unchanged UTC endpoint precision, commit error handling and cancellation without writes. Other themes keep their layout.
- All native tests off-screen. Preserve approved preview work and unrelated Todo artwork. Build a separate release candidate; do not interrupt the running app or publish to Git.

## Review Focus

- Add, Edit, constructor and ShowAsync callers must receive the theme without breaking existing optional arguments or ownerless cancellation.
- New/Back, choosing another project, an empty project list and invalid input must not save stale draft names or falsely report acceptance after a failed commit.
- Calendar text, calendar/dropdown popup keys, local DST boundaries and original UTC precision must keep existing validation semantics.
- Long/RTL names, long durations, expanded errors and short monitors must keep the form usable and footer visible.
- Read-only, missing, overlapping or running records must remain open with readable errors and no duplicate writes; no app settings/history may be touched by test fixtures.

### Task 1: Integrate and verify the native record editor

**Files:** ProjectRecordEditorWindow.axaml/.axaml.cs/new .Navigator.cs; ControllerWindow.Shortcuts.cs; ProjectDashboardWindow.axaml.cs; ProjectRecordNativeChecks.cs and harness Program.cs; native captures/README.
**Interfaces:** Append optional `string? theme = null` to constructor/ShowAsync. Preserve result properties and commit callback. Use the existing shared form styles without changing Timer Editor or Choose Project.

- [x] Add native checks for frame/footer, add/edit and exact UTC results, draft/validation, commit failures and cancellation, popup/native input, themes/RTL and real captures. Build and run `--time-record-native` privately; demonstrate missing frame and relevant behavior failures.
- [x] Integrate the native layout and all callers; keep real controls/validators and fix reproduced issues in their shared handlers. Run focused checks to green and inspect actual native captures.
- [x] Run shortcut and relevant broader regression tests privately, obtain one independent final review and resolve material findings with failing-then-passing checks.
- [x] Build separate Release candidate, document results and show actual captures in the response. Leave the running app untouched; no commit/push.
