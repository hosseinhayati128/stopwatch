# Habitica sync from independent computers

## Confirmed scope

The user requested multiple syncing computers and confirmed that Windows and Ubuntu use **separate local vaults**, and **each computer sends its own items**. This supersedes the previous blanket one-computer/manual-handoff requirement for this setup. Stopwatch-to-Habitica remains one-way; local lists are not mirrored between machines.

## Approach

Use independent clients and independent vault identities with the same Habitica account. This matches the existing implementation's ownership scope: the owner is stored inside each vault, rather than on the Habitica account globally. Each computer keeps its own queue, stable UUIDs, remote links, token storage and enabled state. No coordinator server is needed because the computers do not edit the same linked tasks.

A shared coordinator would be appropriate for clients editing one shared logical task list, but adds hosting and pairing beyond the confirmed scope. Removing ownership checks globally would expose copied/shared vault metadata to duplicate writes; retain those guards and put the existing linked-vault move action under collapsed advanced options.

## Behavior

- Windows and Ubuntu may be enabled concurrently with the same User ID and public client identifier; configure the API token independently on each machine.
- Identical local titles/timestamps in separate vaults represent separate tasks. Never match remote tasks merely by title.
- A computer edits/completes/deletes only its own linked tasks and checklist items. Restart, pause and failure on one computer must not change ownership or queue state on the other.
- Each installation enforces its existing 30-second pacing and persists server-requested retry delays. Both clients obey Habitica account rate limits; there is no distributed reservation of request times across independent computers.
- No automatic import from Habitica, local-list mirroring, shared-live-vault synchronization, coordinator service, credential copying, bot dependency, live API mutations or Git publication.
- Preserve all existing work, including the timer-editor crash fix and the recent credential/paste fixes.

## Verification

Use two integration instances with distinct temporary vaults, distinct local state/secret stores and one thread-safe fake Habitica server. Verify concurrent enable/create; same-text identity separation; isolated edits/completion/checklists/deletion; restart without duplication; pause and per-client rate-limit independence; per-client HTTP spacing; and existing copied-vault ownership guards. Verify clear independent-computer settings text and a collapsed advanced move section. Run existing worker, HTTP and task UI checks, then Windows/Linux publishes. Report Ubuntu runtime/real-account testing limits explicitly.
