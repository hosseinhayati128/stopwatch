# ActivityWatch: Sync & Export native proposal

Design preview for review, not a production implementation. Rendered with native Avalonia controls inside the existing Settings shell and shared frame. All paths, counts and sync results are samples. No live settings, ActivityWatch requests, file exports or background services are used.

## Proposed arrangement

Keep the ActivityWatch tabs and place automatic-sync preferences and report content on the left, with the export destination, Sync today action and latest manual result on the right. Stack these cards in smaller windows; keep Done outside the scroll area.

This card arrangement is the proposed approach, following the approved Connection & Status layer. A single long form would separate the output file from its result; an export wizard would add steps to a recurring action. Neither is needed for these existing settings. The native spike is for visual approval before implementation.

- **Automatic sync:** With stopwatch exports; On a schedule; interval in minutes. Keep the existing 60-minute default and retain the interval when scheduling is off. Integration off pauses the automatic preferences while leaving manual export available.
- **Report content:** App title summaries; browser activity; minimum duration with the existing 15-second default. The title checkbox affects the application table, not all timeline details. The duration setting filters small totals and also affects timeline filtering; it is not simply a delay before recording starts.
- **Export to Markdown:** Show the shared Obsidian / Markdown folder, a Change folder route to that Settings page, and the ActivityWatch file name. Blank uses `ActivityWatch Log.md`; a missing `.md` suffix is added. Sync today updates today's section and preserves other dates.
- **Sync result:** Separate manual-export feedback from Connection & Status. Start untested in this window; show indeterminate progress, success counts and time, write failure, or a report with no entries. Connection success alone does not prove export success.

## Preview behavior

Missing-folder and invalid-filename/filter states disable Sync today and explain the correction. An invalid automatic interval has its own inline message and does not block independent manual export. Pending export disables conflicting fields and repeat actions. Sample folder selection only changes the preview state; its production action should navigate to Obsidian / Markdown.

Other ActivityWatch tabs are disabled in this standalone drawing fixture. Existing production navigation remains untouched. The result is local to this preview window; it does not propose a new persisted sync-history store or a live countdown.

## Implementation notes from current code

The Desktop app already has the preferences and a manual export action, but does not construct `ActivityWatchSyncService` or invoke `ActivityWatchSync.TryAutoSync` after stopwatch exports. The legacy WPF controller has those connections. Wiring and lifecycle checks are required when this layer is implemented; the preview is not evidence that automatic exports currently work in Desktop.

The shared export client can return empty collections after read failures. A zero-count export must not be described as proof of no activity or healthy watchers. Implementation should distinguish failed reads from a legitimate empty report before writing a replacement section. This is a functional integration concern, not something this drawing-only fixture repairs.

Reuse the existing folder, filename, interval and filter settings. Validate export fields before writing; retain preferences through theme restoration. Use actual service results for counts/errors and capture the destination and options for each pending operation so later edits cannot mislabel a completed export.

## Captures and verification

Primary captures: `ready.png`, `synced.png`, `missing-folder.png`. Additional examples cover syncing, write failure, an empty report, integration off, scheduling off, invalid interval and invalid filename. Compact captures show both the preferences and the destination/result. Scale examples cover 70% and 125%, with a 175% text stress capture at compact 125%.

The fixture checks regular 1120×720 and compact 840×600 windows at 70%, 90% and 125%, keyboard focus, field/action validity, busy-state disabling, horizontal fit, enlarged filename text, a fixed Done button and zero SettingsChanged callbacks. The sample AppSettings remains unchanged.

Run only through the private-desktop harness:

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-activitywatch-export-preview -LogPath .superpowers/sdd/2026-11-15-settings-activitywatch-export-preview/native.log
```

The installed app, production Settings page, export services and clock-overlay artwork are unchanged. No live export or Linux runtime is tested by this proposal.
