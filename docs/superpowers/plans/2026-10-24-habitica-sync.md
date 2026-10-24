# Habitica Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Independent client and UI tasks may be delegated against the interfaces below; preserve existing unrelated edits.

**Goal:** Add independent, durable, one-way Habitica task/reminder/checklist sync to Stopwatch Desktop.

**Architecture:** Core owns a bounded HTTP client, immutable sync identities, a mutation journal, account-scoped delivery state, and a serialized worker. Desktop supplies credentials, settings and user actions; shared vault metadata allows an intentional handoff between machines. The Telegram bot is read-only reference material.

**Tech Stack:** .NET 10, C#, Avalonia 11.2.4, System.Text.Json, HttpClient, OS-protected secrets, injected HTTP handlers and clocks.

**Spec:** `docs/superpowers/specs/2026-10-24-habitica-sync-design.md`.

## Global Constraints

- Stopwatch to Habitica only; standalone notes excluded; todo children become checklist items.
- Windows and Ubuntu; one active syncing installation per account/vault; no Python or bot dependency.
- Automated HTTP calls at least 30 seconds apart; 429 retry no earlier than 60 seconds or a longer Retry-After.
- No live account mutations in automated checks; never log, enqueue, export or commit API tokens.
- Preserve the timer-editor crash fix already in this working tree.

## Review Focus

- Timeout after a successful create or completion: read remote state before replay.
- Editing/deleting before delivery or across restart: coalesce revisions and preserve deletion intent.
- Renaming, duplicate timestamps and multiple vault paths: retain immutable IDs and reject ambiguous matching.
- Switching accounts or computers: never send a previous account's work with new credentials.
- Missing keyring, corrupt queue or inaccessible vault: local work remains available and sync status is honest.

## Task 1: HTTP boundary and credentials

Files: new `StopwatchOverlay.Core/Habitica/HabiticaApiClient.cs`, `HabiticaApiModels.cs`, `HabiticaSecrets.cs`; tests in `tools/HabiticaChecks`.

Interfaces: `HabiticaCredentials(UserId, ApiToken, ClientId)`; `HabiticaApiResult<T>(Data, Error, HttpStatus, RetryAfter, OutcomeUnknown)`; remote task/checklist models and create/update payloads; client GET user/task, create/update/delete task, set completion, add/update/delete checklist item. All accept CancellationToken. Secrets expose `Read(account)`, `Write(account, token)`, `Delete(account)` and a persistence/status indication.

- [x] Write failing fake-handler tests for auth headers, endpoints, malformed responses, 429 Retry-After and unknown mutations.
- [x] Implement the fixed-host, redirect-disabled client with bounded timeouts and sanitized errors.
- [x] Implement Windows protected storage and Linux secret-tool storage, with explicit session-only fallback.
- [x] Run client checks; no actual credentials or live HTTP calls.

## Task 2: Durable state, local mutations and worker

Files: new `HabiticaVaultStore.cs`, `HabiticaSyncStore.cs`, `HabiticaSyncWorker.cs`, `HabiticaTaskMutations.cs`, `HabiticaIntegration.cs` in Core/Habitica.

Interfaces for UI: `HabiticaIntegration.Current`; `GetDetails(AppSettings, NoteEntry)` returns immutable ID, optional due time and `List<HabiticaSubtask>`; `Apply(AppSettings, NoteEntry? before, NoteEntry after, DateTime? dueAt, IReadOnlyList<HabiticaSubtask>? children, bool deleted, Func<NoteSyncResult> localWrite)` returns `HabiticaMutationResult(LocalResult, SyncWarning)`; `Configure(settings)`; `EnableHere(settings, bool takeOver)`; `Disable(settings)`; `GetStatus(settings)`; `ImportExisting(settings)`; `TickAsync(settings, ct)`; `TestConnectionAsync(settings, ct)`.

Subtasks are `HabiticaSubtask(Guid Id, string Text, bool Completed)`. Settings add `HabiticaEnabled`, `HabiticaUserId`, `HabiticaClientId`; token is never a serialized AppSettings field. Integration exposes `SetToken(account, token)` and `CredentialStatus` through the protected store.

- [x] Add failing temp-vault tests for standalone exclusion, identity across rename, queued create/update/delete, revision coalescing, account isolation, handoff and corrupt metadata.
- [x] Implement versioned atomic vault metadata plus before/after mutation intents and recovery. Keep enrolled desired revisions/tombstones in the vault, receipt/HTTP deadlines in local delivery state.
- [x] Implement one-call-at-a-time worker phases: resolve alias, create, update managed fields, reconcile checklist IDs, set completion, acknowledge revision/delete. Persist uncertain operations before sending; unknown outcomes require lookup before replay.
- [x] Test 30-second spacing, >=60-second 429 deadlines, restart persistence, auth blocking and delayed network retry with injected clock/handler.
- [x] Verify local-save failure does not enqueue and queue failure is reported without hiding local success.

## Task 3: Desktop settings and lifecycle

Files: new `Controls/HabiticaSettingsPanel.cs`; modify `Views/SettingsWindow.axaml[.cs]`, `Views/ControllerWindow.axaml.cs`, `Core/AppSettings.cs`.

- [x] Add themed settings controls for enable/disable, account/client IDs, masked token, test, sync existing preview, transfer, pending/status/errors.
- [x] Connect settings to the Task 2 interfaces. Keep connection failures visible and do not erase pending work when disabled.
- [x] Drive asynchronous worker ticks while the app is open, including tray mode, cancel in-flight requests on account/vault change or disable; stop on application close.
- [x] Test settings and no-keyring fallback with fake integration/secrets; preserve existing startup and theme behavior.

## Task 4: Todos, reminders and subtasks

Files: modify `Views/NoteEntryWindow.axaml[.cs]`, `TodoOverlayWindow.axaml.cs`, `NotesViewerWindow.axaml.cs`; extend `Core/ObsidianNotesSync.cs` with validated reminder mutations; new `Controls/HabiticaSubtaskEditor.cs` as needed.

- [x] Write failing mutation/UI checks for existing todos, reminders and standalone notes.
- [x] Use the Task 2 mutation wrapper on create/edit/delete/completion; retain Telegram delivery and existing Markdown.
- [x] Add structured subtask editing and floating child checkboxes. Preserve child UUIDs through edits/reordering and keep parent completion independent.
- [x] Enable optional reminder date/time, plus reminder edit/delete actions; do not infer dates from old reminder prose.
- [x] Verify actual labels and task contents after window expansion on Windows; preserve Linux layout path.

## Task 5: Verification, documentation and review

- [x] Run `dotnet run --project tools/HabiticaChecks -c Release` with isolated state and fake HTTP.
- [x] Run Desktop regression checks and existing unit tests sequentially where windows/focus are involved.
- [x] Publish Windows and Linux builds; add the focused cross-platform Core checks to CI and document connection, one-way behavior, subtasks, keyring fallback and manual machine handoff.
- [x] Review the final diff against every spec acceptance case. Report any runtime/API verification limitations accurately; do not claim a live account is connected without a separately authorized live test.

No Git push or real Habitica task changes are part of implementation verification. Integration uses only its own linked items when the user later configures/enables it.

## Implementation and verification record — 2026-10-24

Implemented on `codex/habitica-sync`, preserving the pre-existing timer-editor crash fix. The store/journal/mutation APIs are grouped in `HabiticaIntegration.cs` and the worker in `HabiticaSyncWorker.cs` rather than split into the tentative store filenames above. Remote links and checklist receipts, together with request deadlines, also travel in the vault sidecar so an intentional machine handoff preserves identity and pacing. Installation identity, active scopes and delivery timing remain local; tokens remain in protected storage.

Verification completed using synthetic credentials, temporary vaults and fake HTTP only:

- `tools/HabiticaHttpChecks`: 28 checks passed, including Windows DPAPI persistence, encrypted-at-rest bytes and session fallback.
- `tools/HabiticaChecks`: 30 checks passed, including uncertain create/completion/delete/checklist replies, edits during unknown outcomes, account/handoff separation, rejected-input recovery, explicit missing-task recreation, request deadlines, corrupt metadata and existing todo due dates.
- `tools/HabiticaTaskUiChecks`: 12 checks passed, including reminder mutations, actual editor saves, stable child IDs, standalone-note preservation, visible queue warnings, account-switch guards and dedicated settings navigation.
- Existing `StopwatchOverlay.Tests`: 975 passed, 0 failed/skipped.
- Existing `tools/DesktopRegressionChecks`: all 15 groups passed on Windows, including overlays, shortcuts, contrast, notes alignment and the prior timer-editor fix.
- Windows `win-x64` and Linux `linux-x64` framework-dependent single-file publishes succeeded. Verification outputs: `dist/habitica-check-windows` and `dist/habitica-check-linux`.
- Final independent Core/settings review reported no remaining blocking issues; `git diff --check` passed.

Limits: no authenticated Habitica smoke test and no Ubuntu runtime/keyring/UI session were used. Linux checks are wired into CI with Xvfb, but that remote CI run has not been triggered here. The local Windows build and isolated UI checks are not a claim that Ubuntu runtime behavior has been live-verified. A NuGet vulnerability-feed availability warning appeared during some UI builds; compilation and checks succeeded.

Setup and manual computer handoff are documented in `docs/habitica.md`. Existing installed output/startup registration were left untouched; `./publish.ps1` installs and starts the new Windows build through the project's established path. No commit or push was made.

Follow-up: the API-token field lacked a right-click editing menu. A synthetic keyboard test confirmed Ctrl+V reached the masked field and failed on the missing Paste action before the fix. Added the context-menu action and an embedded Paste button that preserves selection. All 13 task/settings checks now pass, including keyboard/menu/button paste and masking assertions. Paste tests intercept clipboard reads and use synthetic text; they do not read or overwrite the user's clipboard. Rebuild with `./publish.ps1` to install this follow-up.

Further user clarification: both paste methods failed on Windows. The initial route test had bypassed clipboard reading/text normalization and did not establish that user's full failure. Reproduced a blank paste when the single-line TextBox receives a token with leading line breaks. All paste routes now share a bounded asynchronous clipboard handler that trims surrounding whitespace, preserves masking/selection, and reports empty or unreadable clipboard content without clearing an existing token. Tests inject synthetic clipboard-reader results instead of touching the real clipboard; 14 task/settings groups pass, including empty-text and timeout feedback. The exact contents/availability of the user's clipboard were not inspected.

Connection-feedback follow-up: public-ID validation is shared with the HTTP boundary and runs before saving settings or reserving request delays. Rejected secret writes now throw a sanitized error so the token draft is not cleared as if saved. The UI explains why an accepted save clears the token field, distinguishes protected/session storage, and separates missing-token, authentication, network, local-storage and rate-limit feedback. Verified 32 worker groups, 28 HTTP/secret checks and 16 task/settings groups using synthetic credentials; final Desktop build succeeded with zero warnings/errors. No authenticated live Habitica request was made.
