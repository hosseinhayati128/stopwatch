# Settings Obsidian / Markdown Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in the current session. Steps use checkbox syntax for tracking.

**Goal:** Integrate the approved Obsidian / Markdown Settings design with the existing settings, folder picker and history exporter.

**Architecture:** Follow the Application page's reparent-and-restore pattern in a SettingsWindow partial. Keep the named controls and their existing event handlers. Use one destination/status refresh path for field changes, reloads and real export results.

**Tech Stack:** C#, Avalonia, existing native desktop regression harness.

**Spec:** `design/one-piece-v2/settings/obsidian-native-preview/README.md` and its approved images.

## Global Constraints

- Run all native checks on the existing private Windows desktop, without switching desktops.
- Preserve the shared panel frame, clock overlay artwork and existing theme fallback.
- Use sample models and test-owned output folders, never the user's vault or installed app.
- No new packages, persistence schema, export format or background synchronization changes.
- Continue in the existing checkout. Publication and updating the running app remain separate user steps.

## Review Focus

- Theme round trips must restore controls and event wiring without duplicate settings callbacks.
- Blank folders must disable export; editing a destination must clear stale export feedback.
- Export failures and unavailable history must remain visible and recoverable.
- Narrow windows, long paths and enlarged text must preserve scrolling and the fixed footer.
- Manual export with automatic export off must still write completed history through the existing service.

### Task 1: Integrate and verify the page

**Files:**
- Create `StopwatchOverlay.Desktop/Views/SettingsWindow.Obsidian.cs` for layout, restoration and destination feedback.
- Modify `SettingsWindow.Navigator.cs` for page navigation and theme restoration.
- Modify `SettingsWindow.axaml.cs` for real result feedback and destination refresh.
- Create `tools/DesktopRegressionChecks/SettingsObsidianNativeChecks.cs` for callbacks, actual file export, layout and captures.
- Modify `tools/DesktopRegressionChecks/Program.cs` for `--settings-obsidian-native`.
- Update `SettingsObsidianPreview.cs` to restore legacy controls before reconstructing the archived prototype.
- Add implementation evidence under `design/one-piece-v2/settings/obsidian-implemented/`.

**Interfaces:**
- Consume the existing named Obsidian controls, `SettingsChanged(SettingsChangeKind.ObsidianExport)`, and `ObsidianLogSync.SyncHistory`.
- Produce `BuildObsidianPage(Grid host)`, `RestoreObsidianPage()`, `ResizeObsidianPage()` and `SetObsidianStatus(string title, string detail, bool failed = false)`.

- [x] Write native tests for missing-destination gating, preference callbacks, real temporary-folder export, stale-status reset, recoverable failures, reloads, theme restoration, page visibility, keyboard focus and responsive layout.
- [x] Build the fixture and run it privately before implementation. Expect failure because export is enabled without a destination and the new page is absent.
- [x] Implement the approved two-card page, keeping existing named control instances and callbacks. Restore moved controls and styles when leaving the Nika theme.
- [x] Connect export/folder errors and success feedback to the new status card. Disable export for blank destinations; clear obsolete feedback when fields change.
- [x] Adapt the archived preview to the new reparenting lifecycle.
- [x] Build the desktop and harness, then run `--settings-obsidian-native` on the private desktop. Expect all native groups to pass and capture wide/compact views at 70%, 90%, 125%, plus large-text fit.
- [x] Run the existing unit suite privately, plus adjacent Settings shell/Application checks and the archived Obsidian preview.
- [x] Review the scoped diff with a fresh reviewer, resolve consequential findings, save validation evidence and show actual native captures in chat.

No commit or push in this task; the user requests publication separately in this workflow.
