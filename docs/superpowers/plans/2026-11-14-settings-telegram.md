# Settings Telegram Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans in the current session. Track the steps below.

**Goal:** Integrate the approved Connection, Topics and Delivery cards with the existing Telegram settings and actions.

**Architecture:** Reparent the original named controls into a SettingsWindow partial and restore them on theme changes. Keep transport and queue semantics in TelegramNotesSync. Supply its existing optional HttpClient from a constructor dependency so native checks can exercise real handlers with an in-memory HTTP transport.

**Tech Stack:** C#, Avalonia, the existing private-desktop regression harness.

**Spec:** `design/one-piece-v2/settings/telegram-native-preview/README.md` and approved captures.

## Global Constraints

- Keep all native test windows on a private Windows desktop, without switching desktops.
- Test with a separate outbox/vault, sample credentials and an in-memory HTTP handler. Never contact Telegram or send actual messages during checks.
- Retain the original preferences, topic routing, queue persistence, three-day vault scan and confirmation before sending scanned notes.
- Preserve the shared frame, clock overlay artwork, other themes and existing Settings callbacks.
- Do not update the installed app, commit or publish in this implementation task.

## Review Focus

- Token masking must survive navigation/theme changes and credential text must not leak into error feedback.
- Busy actions must not duplicate requests or be re-enabled by outbox events.
- Results from an old configuration or closed window must not overwrite current feedback or send scanned notes after a changed confirmation context.
- Delivery failures must preserve queued entries and remain visible after refresh.
- Reparenting, long paths/status text and enlarged typography must retain focus, fit and the fixed footer.

### Task 1: Implement and validate the page

**Files:**
- New `SettingsWindow.Telegram.cs` for layout, restoration, masking and responsive sizing.
- New `SettingsWindow.TelegramActions.cs` for the existing handlers and their UI states, moved from `SettingsWindow.axaml.cs`.
- Update `SettingsWindow.axaml.cs`, `SettingsWindow.axaml` and `SettingsWindow.Navigator.cs` for lifecycle, constructor dependency and defaults.
- New `tools/DesktopRegressionChecks/SettingsTelegramNativeChecks.cs`, a Program dispatch, shell visibility check and archived preview restoration.
- Add implementation captures and notes under `design/one-piece-v2/settings/telegram-implemented/`.

**Interfaces:** Existing named fields and buttons, `SettingsChanged(SettingsChangeKind.Telegram)`, `TelegramNotesSync.TestConnectionAsync`, `FlushOutboxAsync`, `GetUnsentVaultNotes`, `QueueVaultNotes`, and TelegramOutboxStore remain the behavior boundaries. Add `BuildTelegramPage(Grid)`, `RestoreTelegramPage()`, `ResizeTelegramPage()` and `RefreshTelegramConfiguration()` within SettingsWindow.

- [x] Add native checks for masking/reveal, preference callbacks, reload/theme restoration, blank configurations, real handler/queue behavior through a fake HTTP transport, pending retries, scan confirmation/cancellation, stale async results and window close.
- [x] Build/run the checks privately against the old page. Expect missing masked-token/page behavior, without any real network requests.
- [x] Integrate the three-card layout using the original control instances. Restore styles and parents on theme changes. Use growing editors and automatically sized reveal/action labels.
- [x] Connect real feedback, action gating and revision/closed-window checks. Preserve service semantics and redact the operation token from displayed errors. Test uses the current captured credentials and explicit send behavior.
- [x] Adapt shell and archived-preview fixtures to the extracted page.
- [x] Build and run the new native groups, six scale/size cases and enlarged text. Capture actual native views with fake-service results.
- [x] Run adjacent Settings checks, the archived preview and existing unit suite privately. Expect all passing.
- [x] Obtain one independent read-only review, fix consequential findings with regression checks, record evidence and show the result in chat.

Publication and installing the build remain separate user steps.
