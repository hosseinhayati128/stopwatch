# ActivityWatch Sync & Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the approved Sync & Export design with working desktop automatic sync and reliable manual exports.

**Architecture:** Reuse the ActivityWatch Settings page, shared Obsidian folder and existing export algorithm. Make export reads strict without changing the tolerant Activity & Time Review callers, snapshot export inputs, and serialize/atomically replace Markdown output. Reuse ActivityWatchSyncService for periodic/after-export triggers and integrate its lifecycle in the Desktop controller. Add the approved cards by reparenting existing Settings controls.

**Tech Stack:** C#, Avalonia, xUnit, private-desktop native fixtures. No new dependencies.

**Spec:** design/one-piece-v2/settings/activitywatch-export-native-preview/README.md (approved native preview).

## Global Constraints

- All tests run on the private desktop with fake HTTP and temporary output folders; no live settings, account services, exports or process launches.
- No installed-app update, restart, commit or push in this request. Preserve approved preview files, shared artwork and unrelated Settings functionality.
- Shared folder comes from Obsidian / Markdown. Blank filename uses ActivityWatch Log.md; append .md when needed; reject path traversal, rooted filenames and reserved filenames before writes.
- Preserve 60-minute schedule and 15-second minimum defaults; valid ranges 1–1440 minutes and 1–3600 seconds. Invalid schedule drafts do not block independent manual export.
- Integration off stops automatic work but manual Sync today remains available. Report title choices do not promise to remove timeline titles.
- Failed, malformed, canceled or truncated activity reads must not replace an existing report. A valid empty event list is distinct from a failed read or absent activity watchers.
- Freeze destination/options for each operation. Prevent duplicate exports, keep other dates, use a same-directory temporary file and atomic replacement, and remove temporary files on failure.
- Automatic checks must be bounded and disposed on close. Settings changes invalidate obsolete work. Late callbacks must not change newer status or unlock a newer operation.

## Review Focus

- HTTP errors, malformed or partial event responses must not turn into successful empty reports or destroy prior content.
- Concurrent manual, shortcut and scheduled work must not overwrite each other, and cancellation must respect the atomic-write commit boundary.
- Folder/filename/filter edits, navigation, close and theme restoration must not leave stale success, disabled controls or incorrect destination labels.
- Periodic and stopwatch-export hooks must honor integration/preferences, avoid duplicates and unsubscribe/dispose without touching preview controllers or another settings instance.
- Reparented controls must preserve callbacks, valid preferences, legacy styles, compact scrolling, enlarged text and the fixed Done button.

### Task 1: Reliable export boundary

**Files:** ActivityWatchClient.cs, ActivityWatchSync.cs, ActivityWatchExportTests.cs.
**Interfaces:** optional HttpClient argument at the end of GenerateDailyReportAsync / SyncAsync; optional strict read flag on GetBucketsAsync / GetEventsAsync; shared SnapshotSettings and TryNormalizeExportFileName helpers. Existing callers remain compatible.

- [x] Write tests using fake HTTP and temporary Markdown files; observe RED before implementation.
- [x] Implement strict export reads, stable options, filename/range validation, duplicate guard, cancellation and atomic replacement.
- [x] Verify failures preserve bytes, real empty data succeeds, other dates survive, invalid filenames never call HTTP, pending edits cannot redirect output, and concurrent exports are guarded.

### Task 2: Desktop automatic-sync lifecycle

**Files:** ActivityWatchSyncService.cs, ObsidianLogSync.cs, ControllerWindow.ActivityWatch.cs, ControllerWindow.axaml.cs, ControllerWindow.Shortcuts.cs, ActivityWatchSyncServiceTests.cs, native controller checks.
**Interfaces:** ActivityWatchSyncService accepts optional sync delegate/TimeProvider for isolated checks, adds SyncAfterStopwatchExport; ObsidianLogSync.ExportCompleted reports only successful exports; Desktop subscribes only for its own settings instance and unsubscribes on close.

- [x] Add failing service tests for schedule, flags, snapshots, duplicate guards, reconfiguration cancellation and disposal; add native lifecycle checks.
- [x] Wire the existing service to the Desktop lifecycle and successful stopwatch exports from any existing UI entry point. Route shortcuts through the service while preserving existing shortcut enablement.
- [x] Verify timers using a fake TimeProvider and verify export hooks with sample settings only.

### Task 3: Sync & Export Settings cards and result states

**Files:** SettingsWindow.ActivityWatch.cs, new .ActivityWatchExport.cs / .ActivityWatchExportActions.cs, SettingsWindow.axaml.cs, Program.cs, SettingsActivityWatchExportNativeChecks.cs, implemented captures/README.
**Interfaces:** reuse existing named controls and HTTP seam. Add named production export grid, status, folder route and validation hints; cancel manual operation on page/tab/theme/close or changed export configuration.

- [x] Write native checks for layout, callbacks, retained tabs/themes, folder route, validation, manual result/error/empty states, busy/stale/canceled results; observe RED.
- [x] Build the approved cards and actions, preserving existing control callbacks and restoring legacy properties.
- [x] Run native captures privately at 70%, 90%, 125%, regular/compact, plus 175% text; inspect the result here. Verify the archived preview still works.
- [x] Run full unit suite and adjacent ActivityWatch/Settings regressions, obtain one fresh final review, resolve material findings with focused RED/GREEN checks, and leave work uncommitted.
