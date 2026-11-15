# Internet History & Export Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for native execution. Track the steps below and keep tests on private Windows desktops.

**Goal:** Implement the approved History & Export Settings layer and correct clear-history persistence and reporting.
**Architecture:** Reparent existing fields/actions into the current Internet page. Keep file work on a worker, snapshot settings for each operation and reject obsolete UI completions without claiming already-started writes were canceled. Reuse the core sync lock to coordinate automatic samples, manual sync and explicit clear.
**Tech stack:** Existing Avalonia/.NET 10, xUnit and private-desktop native fixture.
**Spec:** design/one-piece-v2/settings/internet-export-native-preview/README.md, approved in this chat.

## Global constraints
- Preserve Connection & Monitoring, shared art, legacy themes, ordinary import/row-note behavior and monitoring preferences.
- Retention remains all/30/14/7 days, applied during sync. Blank filename uses Internet Log.md. Resolve only a valid filename under the selected folder; reuse the existing filename validation policy.
- No installed app updates or Git publication. Fixtures use synthetic data and temporary stores only; the existing full suite includes its network-probe smoke test.
- Keep the current checkout. Preserve the approved uncommitted prototype. Commit/publish follows a separate user request.

## Review focus
- Failed clear must not lose the local store, silently resurrect checks from Markdown/backup, or report a complete success for a partial outcome.
- A queued operation must use the confirmed destination even if settings change; obsolete completions must not overwrite a newer page state or unlock another operation.
- Automatic append, manual sync and clear serialize through one lock; filename validation runs at the write boundary too.
- Theme rebuild/navigation/close must dismiss confirmation, restore existing controls once and keep in-flight writes honest.
- Count loading and filesystem work must not block the UI; long/RTL paths, large text, keyboard focus and the fixed footer must work at 70/90/125%.

### Task 1: Coordinated safe core export and clear
Files: InternetLogSync.cs, InternetHistoryStore.cs, InternetSyncTests.cs; update legacy WPF clear caller to the corrected shared API.
Interfaces: `InternetLogSync.TryNormalizeFileName(string?, out string)`, `TryGetTargetPath(AppSettings, out string, out string)`, `ClearHistory(AppSettings)` returning `InternetClearResult(LocalHistoryCleared, ReportCleared, TargetFilePath, Message)`; add nullable `CheckCount` to InternetSyncResult without breaking its constructor. `InternetHistoryStore.TryClear(out string?)` persists empty primary and backup before changing the cache, reporting errors.
- [x] Add failing tests for explicit clear/no re-import, backup reload, blocked report preserving local history, failed local persistence, missing folder/local-only clear, safe filenames, and normal import/notes/retention.
- [x] Implement report-first clear under the existing sync lock. A report failure leaves local history intact; a subsequent local failure is reported as partial. Never call ordinary import during clear. Use atomic temporary-file replacement and cleanup.
- [x] Validate sync targets before writes, preserve regular import semantics, fail rather than overwrite an unreadable report. Correct the second existing clear caller in WPF.
- [x] Build/run the full unit suite privately; record failures and results.

### Task 2: Native page, asynchronous actions and lifecycle
Files: new SettingsWindow.InternetExport.cs / InternetExportActions.cs; narrow edits to Internet.cs, InternetActions.cs, axaml.cs and Navigator.cs; new SettingsInternetExportNativeChecks.cs plus Program.cs route.
Interfaces: `BuildInternetExport`, `RestoreInternetExport`, `RefreshInternetExport`, `LoadInternetHistoryAsync`, `RunInternetExportAsync(bool clear)`, `DismissInternetExport`; internal SettingsWindow overload taking isolated read/sync/clear delegates while preserving existing constructors.
- [x] Add native failing checks for page controls, temporary-file sync/clear, confirmation cancel and configured target, invalid names, missing folder, pending/stale operations and unchanged monitor preferences.
- [x] Reparent file/retention/sync/clear controls; add history summary, folder navigation, status and inline confirmation. Match approved cards; focus Cancel first and require explicit confirmation.
- [x] Run reads/writes on workers. Keep one operation owned until it really finishes; navigation dismisses confirmations and invalidates display, not disk writes. Disable conflicting edits; commit valid filenames on blur/Enter, retaining the prior value on invalid drafts.
- [x] Refresh history only when the tab opens or a saved background check completes, with no extra polling timer. Restore old control parents/styles for legacy themes.
- [x] Verify native states and six size combinations privately, capture production results and check neighboring Connection & Monitoring behavior. Isolate the existing connection fixture's history read.

### Task 3: Evidence and final review
- [x] Preserve archived preview artifacts while verifying preview compatibility and neighboring Settings behavior.
- [x] Fresh whole-change review under executing-plans; fix Important/Critical findings once with RED→GREEN coverage and a green suite.
- [x] Inspect final native captures; write implementation README and verification receipt, record source/image hashes and confirm installed binary unchanged.
