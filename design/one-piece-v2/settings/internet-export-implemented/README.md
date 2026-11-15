# Internet & Network — History & Export implemented

Native captures of the production SettingsWindow using synthetic history and isolated export actions. Every GUI check ran on a private Windows desktop. Real file export and clear were checked only against temporary test stores. No installed app, user settings, user history or vault file was changed.

The approved Saved history and Export to Markdown cards now use the real Settings controls. The page loads local counts and dates in the background, retains the existing all/30/14/7-day options, links to the shared Obsidian folder settings, validates the file name, and shows pending, success, empty, missing-folder and failure states. Valid file names apply on Enter or leaving the field. Invalid drafts keep the saved value. Blank uses Internet Log.md and a missing .md extension is added.

Retention is applied during sync and affects local history as well as the report; merely choosing an option does not delete records. Normal sync still imports recognized checks from the selected Markdown report and preserves row notes. Manual connection tests remain display-only; automatic checks keep the existing save/export behavior. The page adds no polling timer or export scheduler.

Clear now opens an inline confirmation naming the actual destination and focuses Cancel. A dedicated shared clear operation replaces the old clear-then-sync sequence, which could re-import deleted checks. It empties the report first, checkpoints valid local records to the primary file, then persists empty local backup and primary stores before replacing the in-memory history. The checkpoint protects history recovered from a backup if the primary is damaged or locked. If the report cannot be emptied, local checks remain. If local persistence fails after the report is emptied, the page reports that partial result explicitly. Without a configured folder, confirmation explains that only local history can be cleared; existing Markdown copies remain unchanged.

File operations run off the UI thread and serialize with automatic writes through the existing core lock. Their destination is captured before starting. Navigation and theme changes dismiss confirmations and invalidate old display results, while an already-started write retains ownership until it actually finishes. Closing the window ignores its late completion; it does not claim to cancel a disk write.

## Captures

- `wide-70.png`, `wide-90.png`, `wide-125.png`: 1120 × 720 native windows at the requested scales.
- Matching `-export.png` files show the lower card when scrolling is needed. Done remains fixed.
- `compact-*`: 840 × 600 windows at all three scales.
- `clear-confirmation`, `clear-partial`, `missing-folder`, `invalid-file`, `empty-history`, `syncing`, `synced`, `write-failed`, `retention`, `long-path`, `large-text`: additional production states. Large text uses 125% page scale and 175% text.

## Verification

- Desktop/native, unit and legacy WPF builds: zero warnings or errors.
- Full unit suite: 1,117 passed, zero failed/skipped. The existing suite includes a network-probe smoke test.
- Internet export native checks: three groups pass, covering real temporary-file actions, validation/lifecycle and responsive layouts/theme restoration.
- Neighboring Connection & Monitoring: four groups pass. ActivityWatch export: five groups pass.
- Fresh final review: one important durable-backup finding fixed with a failing regression followed by a green full suite. No critical or minor findings. Existing ordinary-sync retention/persistence and monitor locking remain unchanged; a failed normal sync can still have applied retention, and monitor reconfiguration can wait on a background save.
- Approved design preview: six scale/window combinations pass. Generated neighbor/prototype artifacts were restored to their baseline hashes; fresh regression captures are retained separately in the verification workspace.

Run `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-internet-export-native` with an existing repository-local log directory. Logs and review evidence are in `.superpowers/sdd/2026-11-16-settings-internet-export/`; `verification.json` records source and image hashes.

Live user-vault operations, Linux rendering and release packaging were not tested. Installation and Git publication remain separate user-requested steps.
