# ActivityWatch — Sync & Export implementation

Production Avalonia Settings controls, rendered with isolated sample settings and fake HTTP on a private Windows desktop. Captures are not image-generation mockups.

- Automatic sync: with successful stopwatch Markdown exports or on a schedule (1–1,440 minutes, while Stopwatch is open). Integration off pauses automatic work; manual Sync today remains available.
- Report content: app title summaries, browser activity, and a 1–3,600-second minimum. Timeline details can still contain titles.
- Shared export folder: Change folder opens Obsidian / Markdown settings. Blank filename uses ActivityWatch Log.md; .md is added when needed. Unsafe filenames are rejected.
- Manual result states: ready, busy, success with counts/time/filename, empty report, missing folder, read failure and write failure. Status describes manual work in this Settings window, not persisted automatic-sync history.
- Failed, malformed, truncated or canceled reads preserve the previous report. Exports snapshot options, reject concurrent writes to the same file, preserve other dates and replace the file atomically.
- The page scrolls with a fixed Done button; cards stack in compact layouts. Long folder paths have a full-path tooltip.

## Evidence

`ready.png`, `synced.png`, `syncing.png`, `read-failed.png`, `write-failed.png`, `empty-report.png`, `missing-folder.png`, `invalid-file.png`, `invalid-interval.png`, `ready-70.png`, `ready-90.png`, `ready-125.png`, `compact-settings.png`, `compact-export.png`, `large-text.png`.

Native checks cover real production controls at 70%, 90% and 125%, regular/compact windows, and 175% text; validation/persistence, folder routing, native/legacy themes, stale responses, cancellation, file preservation and controller subscription/disposal. Unit tests cover strict HTTP reads, atomic file writes and deterministic scheduling.

Build/run through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` with `--settings-activitywatch-export-native`. Adjacent connection and Obsidian checks, archived design preview, and full unit suite are part of verification.

No installed-app update, live vault export or Git publication is part of this implementation request. Live server/watcher behavior and Linux rendering remain unverified.

Verified 2026-11-15: build 0 warnings/errors; 1,084 unit tests; 5 export native groups, 4 connection groups, 3 shared-folder groups, and 6 archived-preview size cases passed off-screen. Final review findings (required event duration and legacy sibling ordering) were reproduced and fixed with RED→GREEN regressions.
