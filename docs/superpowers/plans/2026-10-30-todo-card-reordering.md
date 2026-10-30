# Todo card reordering implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in the current checkout. Steps use checkbox tracking.

**Goal:** Make Manual sorting easy to use: drag a task card and hold near either list edge to scroll continuously, then drop at a clear insertion position.

**Architecture:** Keep the existing organization store and completion groups. Move drag interaction into a focused TodoOverlayWindow partial with pointer capture, a drag preview and insertion line, and a short UI timer for edge scrolling. Title dragging and a larger accessible handle share the same session.

**Tech stack:** .NET 10, Avalonia Desktop, existing native regression runner.

**Spec:** The user's 2026-10-30 request in this chat: Trello-like card movement in Manual mode, including scrolling upward while holding a card at the top edge of a long list. Bottom-edge scrolling is the symmetric behavior.

## Global constraints

- Preserve the approved Classic/Pixel appearance and existing alternatives.
- Only Manual mode reorders; keep completion checkboxes, links, priority controls and subtasks interactive.
- Persist once on drop, preserving priority and completion groups; no production task/settings writes during tests.
- Cancel on Escape, capture loss, closing/hiding, changed settings or sort mode. No publishing or Git publication in this task.
- Reuse the existing codex/nika-chronicle checkout and preserve all prior uncommitted appearance work.

## Review focus

- A stationary pointer near either edge must keep scrolling, including after several timer ticks.
- Gaps, tall expanded cards and the ends of a list must produce a stable insertion point.
- Holding/dragging titles must not complete tasks, and links and nested controls must keep their own actions.
- Cancelling or releasing outside must stop scrolling and leave the saved order unchanged.
- Hidden completed tasks, changed task files and a changed notes folder must not produce an unintended reorder.

## Task 1: Native drag session and edge scrolling

**Files:** Create `StopwatchOverlay.Desktop/Views/TodoOverlayWindow.Reordering.cs` and `tools/DesktopRegressionChecks/TodoReorderChecks.cs`; update the window's existing code-behind, XAML and regression runner switch.

**Interfaces:** Preserve `CreateDragHandle`, `MoveTodo` and `CancelDrag`. Add `AttachCardDrag` for title/background input. One session owns the pointer, source row, saved context, drop target, preview, and timer.

- [x] Add isolated routed-pointer tests for stationary edge scrolling and saved order; run `--todo-reorder-only` against the old implementation and observe failure.
- [x] Implement title/handle drag, a 5-DIP activation threshold, insertion feedback including gaps, and 48-DIP edge zones with proximity-based scrolling capped at 700 DIP/second. Retain the existing keyboard handle controls.
- [x] Add coverage for top/bottom travel, neutral/outside pointer positions, Escape/capture-loss cleanup, short title clicks, priority mode, completion groups and settings changes. Verify task content and real settings remain unchanged.
- [x] Run the focused native reordering suite and existing appearance/input suite; inspect a native drag capture.
- [x] Obtain a fresh read-only review of the task changes, fix actionable findings, and record final verification. Leave source changes local alongside the existing work.

## Verification

The old implementation failed the stationary bottom-edge scrolling test. The final build has zero warnings/errors. `--todo-reorder-only` passes stationary top/bottom scrolling, gap insertion and persisted order, title holds without completion, short presses, Escape/capture loss/outside release, keyboard handling, settings/sort changes, expanded cards, stale completion changes, long-distance drops across Classic/Pixel/One Piece/Daylight, the top scroll limit and hiding cleanup. Test fixtures verify task Markdown and real user settings are unchanged.

`--todo-appearance-only` also passes, including existing completion/cancellation behavior and native appearance checks. A fresh read-only review reported no actionable findings. The reviewer did not independently run physical mouse tests; validation used routed native pointer events and actual Avalonia windows. No publishing, app restart, migration or Git publication was performed.
