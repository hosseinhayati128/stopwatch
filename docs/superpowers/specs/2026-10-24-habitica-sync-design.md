# Habitica integration for Stopwatch Desktop

Status: approved by the user on 2026-10-24 ("good continue").

Update: the computer limitation below is superseded for independent local vaults by `2026-10-24-habitica-multiple-computers-design.md`. The user subsequently confirmed separate vaults and one-way uploads of each computer's own items. Exclusive ownership remains scoped to a single linked/copied vault, not the entire Habitica account.

## Intent and agreed scope

Connect Stopwatch directly to Habitica so the user can manage tasks in Stopwatch and have their changes reflected in Habitica. The installed app must not depend on Python, the Telegram bot, or the bot's source directory. Support the current Avalonia Desktop app on Windows and Ubuntu; keep the legacy WPF project buildable.

The user confirmed:

- Sync direction is Stopwatch to Habitica only.
- Creating, editing, and deleting a todo or reminder must update its linked Habitica task.
- Standalone notes must not be sent to Habitica.
- Subnotes belonging to a todo become checklist items under that Habitica todo.
- Rate-limited operations must retry automatically, approximately one minute later.
- Only one computer needs to run Habitica sync at a time; simultaneous Windows/Ubuntu sync is outside the first version.

Implementation defaults proposed below are builder decisions, not additional user-confirmed requirements.

## Approaches

The selected recommendation is a C# Habitica client and background sync worker packaged inside Stopwatch. Existing `Habitica_API.py` demonstrates the API contract, authentication, error classification, and checklist endpoints; its Python implementation is reference material only.

Calling the Telegram bot would introduce a deployment and availability dependency that conflicts with the request. A separate companion service would require installing and maintaining another program on every machine. Neither is needed when the existing .NET app can call Habitica directly.

## Existing code and boundaries

- `StopwatchOverlay.Core` contains platform-independent note persistence, todo organization metadata, Telegram outbox handling, and HTTP sync logic.
- `NoteEntryWindow` saves local entries and dispatches new Telegram messages. Todo edits pass through `TodoOrganizationStore.SaveTodo`.
- `TodoOverlayWindow` handles completion, editing, deletion, and Telegram synchronization. `NotesViewerWindow` also exposes task interactions.
- `NoteEntry` currently identifies entries using type, timestamp, and text, rather than an immutable UUID. Todo organization metadata already retains aliases across edits.
- Reminders currently store text; their popup does not expose the structured due-time controls available for todos. Reminder editing/deletion also needs explicit mutation support.
- Todo subnotes are not currently a separate structured child-task model. Existing free-form paragraphs and bullet lists must retain their current meaning.

Do not overwrite unrelated local changes, particularly the uncommitted timer-editor crash fix. Do not modify or deploy the Habitica Telegram bot.

## User-visible behavior

### Connection settings

Add a Habitica section in Settings, following the existing theme and typography controls. Include enable/disable, User ID, a masked API token, Test connection, pending-change count, last successful sync, and a sanitized error/status message. Explain that this version sends changes outward only. Do not import or overwrite unrelated existing Habitica tasks automatically.

Use a valid public, author-owned `X-Client` identification, `<author-Habitica-UUID>-StopwatchOverlay`, supplied through app configuration/advanced settings. Never use the bot's obsolete `habitica-telegram-bot` fallback. User credentials remain per account, and the public client identifier is not an API token.

The worker runs while the app is open, including minimized to tray. Pending work survives exit and resumes at the next launch; this does not install a separate background service.

### Todos, reminders, and checklist items

- A todo maps to a Habitica `todo` task. Its title/body, explicit due date, and completion state are synchronized. Ordinary body paragraphs stay task notes, not subtasks.
- A reminder also maps to a Habitica `todo`, identified as a reminder through an app-managed tag or explicit description. Add optional structured due date/time to its editor, using the existing todo date/time behavior. Preserve old reminder text without attempting to infer dates from prose. Store the schedule explicitly; do not promise Habitica notifications without confirmed support from its clients.
- Add an explicit subtask/subnote checklist section to the todo editor, with add, edit, delete, and checked state. Render these child items under the parent in the floating overlay. Each child maps to a Habitica checklist item, not an independent top-level todo.
- Parent completion and child completion are independent. Checking every child does not automatically complete the parent. Merely reaching a reminder's due time does not mark it complete.
- Local urgency priority and Habitica difficulty are different concepts. Keep Habitica's default difficulty in the first version rather than silently mapping urgency to rewards.

Local saving must remain responsive and must succeed without a Habitica connection. Remote status distinguishes pending changes from confirmed synchronization.

### Existing items

By default, only new items and changes made after enabling the integration are enrolled. Provide an explicit Sync existing items action with a preview/count for the user to confirm. Do not automatically attach local entries to Habitica tasks just because their titles match. Deleting a linked parent deletes that linked Habitica task and its checklist only; no account-wide cleanup.

## Identity, persistence, and mutation capture

Add shared vault metadata for an immutable vault UUID, immutable parent and child UUIDs, desired revisions, and alias migration from the current timestamp/text keys. A parent retains its UUID across title, date, priority, and completion changes. Child identity also survives edits and reordering. Keep these identities with the vault so copying the vault between Windows and Ubuntu does not create a different logical task identity; do not derive the identity from an OS-specific absolute path. Existing note Markdown should not be rewritten merely to insert opaque IDs. Structured child content lives in this metadata in the first version; existing Markdown paragraphs and lists remain untouched.

Keep account-specific Habitica links, last acknowledged revisions, and a durable outbox in an atomic local sync store. Keep enrolled account identities, latest desired state, and deletion markers in the shared vault metadata so another installation can reconstruct pending work during an intentional transfer. Namespace links and jobs by Habitica user and vault identity. Never redirect old queued work to a newly selected account or vault. Remote aliases should derive from stable item UUIDs to support recovery of uncertain creates when changing machines.

### One active syncing installation

The user chose one active syncing computer at a time. The sync enable switch and machine identity are installation-local and must not automatically enable a second worker when settings/vault files roam. Record the designated installation in shared account/vault metadata. A second installation can use local tasks normally but leaves Habitica synchronization off until the user explicitly transfers it after stopping sync on the first computer.

On transfer, first reconcile enrolled desired revisions and deletion markers with remote task aliases, then rebuild local pending work. In-flight mutations with unknown outcomes must be resolved before any replay. Clearly explain the manual handoff in Settings; replicated files are not a distributed lock, and the first version does not promise correctness when two machines are deliberately allowed to sync the same account/vault simultaneously. No server or companion service is introduced for coordination.

Capture successful local create, update, delete, and completion operations from a common core mutation layer used by the relevant UI entry points. Extend the existing validated todo mutation contracts for reminders without changing standalone-note behavior. Save a durable mutation intent before changing local files, then finalize it against the resulting local state; recover unfinished intents on restart. A failed local mutation must not be sent remotely. If durable sync storage cannot be written, show that the local change is not queued instead of claiming it will sync.

Coalesce pending revisions of the same item to its latest desired state. Creating and deleting an item before its first confirmed upload should cancel the pending create when no uncertain delivery is involved. Retain deletion markers until the remote deletion is confirmed. Do not infer remote deletes from a missing vault, an unreadable file, or ambiguous duplicate local entries. External Obsidian edits, filesystem watching, and importing unrelated Habitica changes are outside this first version.

## HTTP client and retry worker

Use .NET `HttpClient` in Core with bounded timeouts and cancellation, validated response shapes, and sanitized errors. Send authentication only to the fixed official HTTPS Habitica API; disable automatic redirects for authenticated requests. No bot processes or calls to its Flask routes.

The reference bot already demonstrates:

- `POST /api/v3/tasks/user` for creation.
- `GET`, `PUT`, and `DELETE /api/v3/tasks/{id-or-alias}` for lookup, edits, and deletion.
- Checklist create/update/delete endpoints and task completion scoring endpoints.

Run one serialized worker per account in the designated installation. Habitica's official guidance requires at least 30 seconds between automated background API calls, including reads. Persist the earliest next-call time across restarts. Use aliases, revision tracking, preflight reads, and uncertainty handling to prevent blind duplicate mutations after restart or an intentional machine handoff.

For HTTP 429, leave work pending and schedule no earlier than the later of 60 seconds from the failed attempt and Habitica's valid `Retry-After` deadline. Honor any later confirmed reset deadline. A response with zero remaining allowance must also delay the next call. Do not hold up the UI thread.

For offline/network errors and transient server failures, retry with increasing delays starting at one minute, capped at fifteen minutes, with small positive jitter. Invalid credentials, missing configuration, or rejected input block affected work with an actionable status; do not retry these every minute. Disabling sync stops new HTTP calls without discarding pending changes.

## Avoid duplicate tasks and rewards

Never blindly replay a mutation whose outcome is unknown after a timeout, disconnect, or ambiguous server failure. Reconcile through a rate-limited read first:

- Resolve the stable task alias before repeating an uncertain create. If identity cannot be established unambiguously, retain a needs-attention status instead of creating another task.
- Updates converge on the latest desired revision, while preserving fields this app does not manage.
- A confirmed missing task can satisfy a pending delete. An unexpected missing task during an update requires a visible relink/recreate decision rather than silently resurrecting a deletion.
- Completion scoring can award rewards. Read its current completed state before repeating an uncertain score request; do not award completion twice. If the task can no longer be read, stop for review.
- Prefer checklist endpoints that set an explicit completed value instead of blindly replaying a toggle. Resolve missing child IDs from the linked parent before retrying an uncertain child create.

The app does not change unrelated tasks, Habitica cron, habits, dailies, rewards, bot configuration, or other account gameplay state.

## Credentials

Do not read credentials from the bot's pickle/runtime data or copy its secrets into this repository. Credentials are entered in Stopwatch settings and kept out of queued payloads, logs, exports, screenshots, and source control. Use OS account secret storage where available: Windows protection and Ubuntu's desktop secret service; when a supported secret store is unavailable, keep the token in memory for the session rather than silently writing plaintext. Document this behavior in the connection UI.

## Testing and acceptance

Use injected HTTP handlers, clocks, persistence paths, and secret stores. Automated tests must not call the user's Habitica account or modify their actual vault.

Required cases:

1. New todo/reminder creates exactly one linked task; edits and deletion target only that task.
2. Standalone notes produce no API work; explicit child items create/update/delete/check the corresponding checklist entries.
3. Local changes survive offline operation, restart, rapid edits, and create-then-delete sequences.
4. 429 retries wait at least one minute and respect longer `Retry-After`; automated requests remain at least 30 seconds apart.
5. Unknown create, delete, completion, and child-item outcomes reconcile before any repeat mutation.
6. Account/vault switches and intentional machine handoffs do not misroute queued changes or silently create duplicate links; a non-designated installation does not automatically start a worker.
7. Local save/queue failures and invalid credentials remain visible and recoverable.
8. Existing Obsidian formatting, Telegram sync, completion behavior, themes, and typography remain functional.
9. Settings, task editor, reminder editor, and overlay interactions work on Windows and Ubuntu; validate both builds and run available platform UI checks.

Only a separately authorized, deliberately chosen test item may be used for a live API smoke test after configuration. A passing mocked client test is not a claim that the user's account is connected.

## References

- Existing bot API boundary: `Habitica-Telegram-Bot-main/Habitica_API.py` (read-only reference).
- Official endpoints and task/checklist schema: https://apidoc.habitica.com/
- Background-call spacing, identification, and rate-limit behavior: https://github.com/HabitRPG/habitica/wiki/API-Usage-Guidelines
