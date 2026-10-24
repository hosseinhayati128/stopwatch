# Habitica connection

In the Desktop app, open **Settings → Habitica**. Enter your Habitica User ID and API token from Habitica's API settings, plus a public author-owned client identifier in the form `<author-Habitica-UUID>-StopwatchOverlay`. Save the connection, test it, then enable sync on this computer. The token is masked and excluded from ordinary app settings, the vault and Git.

Paste the API token with **Ctrl+V**, the field's right-click **Paste** menu, or its visible **Paste** button. All three keep the token masked.
Surrounding spaces and blank lines are removed before insertion. Empty or unreadable clipboard content produces a visible message and leaves the current token intact.

The token field intentionally clears after an accepted save. Leaving it blank keeps any previously stored token; it does not delete it. The storage status says whether it is Windows account-protected, in the desktop secret service, or session-only. A rejected token write is reported and the draft remains in the field. A saved configuration alone does not prove the API token is valid; use **Test connection**.

The public client identifier requires the tool creator's full Habitica User ID followed by `-StopwatchOverlay`. The name alone or the UUID alone is incomplete. Invalid identifiers are explained before saving or making a connection request. Connection feedback distinguishes an unavailable token, an authentication rejection, network failure, local storage failure and rate limiting.

Stopwatch sends newly created or edited todos and reminders after enablement. **Sync existing items…** previews the count and requires confirmation. Standalone notes stay local. Use the task editor's explicit subtask controls for checklist items: ordinary multiline task descriptions remain descriptions. Parent and child completion are independent. A reminder becomes a Habitica todo with its optional due date; this does not promise a Habitica push notification at the selected time.

This version sends changes **from Stopwatch to Habitica only**. It does not import tasks or match unrelated tasks by title. Editing the Markdown directly is not a sync action; make synced edits in Stopwatch. Other Habitica task fields are preserved. Local save failures do not create remote work. If local work succeeds but metadata cannot be saved, Stopwatch shows a sync warning; keep the vault accessible and resolve that warning before continuing task edits.

The app must stay open (tray mode is sufficient) to deliver queued changes. Each installation spaces its automated API requests at least 30 seconds apart, so large imports take time. Habitica applies account/IP rate limits across clients: each client waits at least a minute or a longer server-requested delay when limited. Network failures use increasing delays. Restarting, testing the connection or moving a linked vault does not bypass its saved request deadlines. After uncertain writes, the worker reads the linked task before retrying. Ambiguous or missing remote links stop with a status message instead of silently duplicating tasks.

## Credentials on Windows and Ubuntu

Windows stores tokens using user-scoped DPAPI. Ubuntu uses the system secret service through `secret-tool` (`sudo apt install libsecret-tools`, with an unlocked login keyring). If protected storage is unavailable, the app explicitly reports **session-only** storage: enter the token again after restarting. Do not copy tokens into the vault or repository.

## Using Windows and Ubuntu together

Both computers can sync concurrently to the same Habitica account when they use **separate local vaults**. Set up Settings → Habitica on each computer with the same User ID/public client identifier, enter the API token separately, and enable sync on both. No manual handoff or coordinator server is needed. Use **Sync existing items…** separately for each computer's older items if desired.

Each computer keeps its own local tasks, task UUIDs, remote links, queue, enable/disable state and protected credentials. Edits, completion and deletion affect only the originating computer's linked Habitica tasks. Equal titles/timestamps on different computers remain separate tasks. Tasks from the other computer appear in Habitica; they are not imported into the local Stopwatch list. Pausing or restarting one computer does not transfer ownership away from the other.

## Moving an already linked vault (advanced)

Do not copy `.stopwatch-habitica.json` between independent vaults. Keep each vault's own metadata file: it holds stable task/checklist identities, links and pending work. The vault identity is stored in that file rather than derived from its Windows/Linux path; copying it copies the links too.

If deliberately moving an existing linked vault, stop its old installation, finish copying, then open **Copied or shared vault (advanced) → Move this linked vault here…** and confirm. This preserves those remote links rather than creating a second set. This procedure applies only to that moved vault; unrelated local vaults on other computers can continue syncing.

Do not run simultaneous writers against the same copied/shared metadata file. File-sync tools are not distributed locks. Corrupt metadata is preserved for recovery and never reset automatically. Account changes do not retarget another account's queued tasks.

If Habitica reports that a previously linked task is missing, it is paused for review. After checking Habitica, use **Recreate missing linked tasks…** in Settings and confirm the displayed count to authorize recreation. Ordinary task edits do not silently recreate deleted remote tasks.

Implementation verification uses temporary vaults, fake HTTP handlers and synthetic credentials. A real account connection is established only when you configure it yourself.

API behavior follows the [Habitica API documentation](https://apidoc.habitica.com/) and [usage guidelines](https://github.com/HabitRPG/habitica/wiki/API-Usage-Guidelines).
