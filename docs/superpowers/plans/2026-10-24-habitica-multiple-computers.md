# Multiple independent Habitica computers implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this focused plan and verify every stated result.

**Goal:** Support simultaneous Windows and Ubuntu uploads of their own local items into one Habitica account, without a manual handoff.

**Architecture:** Retain the existing per-vault ownership and UUID linking. Add two-client regression coverage against a shared fake server and replace the misleading account-wide limitation in settings/docs. Keep copied-vault migration as a collapsed advanced action.

**Tech Stack:** .NET 10 Core, Avalonia 11.2.4, injected HttpClient handlers, temporary JSON/Markdown persistence.

**Spec:** `docs/superpowers/specs/2026-10-24-habitica-multiple-computers-design.md`.

## Global constraints

- Separate local vaults, each computer sends only its own items; no two-way import.
- Synthetic credentials and fake HTTP only; no live account changes or user-vault mutations.
- Preserve existing changes and shared/copied-vault safety guards.

## Tasks

- [x] Add `tools/HabiticaChecks/MultiComputerChecks.cs` and invoke it from `Program.cs`; prove concurrent clients, identity separation, isolated mutations, restart, pause/retry independence and per-client pacing.
- [x] Add a failing settings check in `tools/HabiticaTaskUiChecks/Program.cs` for explicit separate-vault support and collapsed advanced move options.
- [x] Update `HabiticaSettingsPanel.cs` to explain simultaneous independent clients; move legacy transfer action into the collapsed copied-vault section. Scope ownership messages in Core to the selected vault.
- [x] Update `docs/habitica.md` and README; mark the old design's computer limitation superseded for independent vaults.
- [x] Run worker/HTTP/UI checks, publish Windows and Linux sequentially, review scope and record actual limits. Do not push or reconfigure the user's live app.

## Execution record

Scope confirmed by the user's answers: separate local vaults; each computer sends its own items. Ruling: retain the existing per-vault owner/UUID implementation, because two independent integration instances already satisfy that scope. Do not remove copied-vault safety guards or add an unnecessary coordinator service. The prior blanket one-computer guidance was too broad.

The added settings assertion failed before the copy/advanced-options change and passed afterward. `tools/HabiticaTaskUiChecks` now passes all 17 groups. The full worker/recovery suite and new shared-fake-server multi-computer scenarios pass: concurrent enable/create, same-title identity separation, isolated edits/completion/checklists/deletion, restart without duplicate creation, pause/offline independence, per-client rate delays and HTTP spacing. Existing same-vault ownership/takeover checks remain green. `tools/HabiticaHttpChecks` passes all 28 transport/protected-secret checks.

Windows `win-x64` and Linux `linux-x64` framework-dependent single-file publishes succeeded, in `dist/habitica-check-windows` and `dist/habitica-check-linux`. The fresh focused review found no blocking defects; `git diff --check` passed. The “Windows” and “Ubuntu” test clients are simulated installations on this host, not a live Ubuntu runtime test. No live Habitica requests, real vault changes, credential reads, startup changes, Git commits or pushes were made for this change.
