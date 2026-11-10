# Timer Editor — native integration

The approved Nika editor now reuses the real editor fields, handlers and return values inside the shared Engraved Filigree frame. Other themes retain their previous layout. No new artwork, dependencies, settings or refresh timers were added.

The header and Save/Cancel/Undo footer stay fixed while fields and records scroll. Preferred size is 620 × 780 DIP; available monitor space limits the initial height. Captures here reflect the actual smaller private-desktop work area rather than forcing the preview size. Drag the heading to move; drag the lower-right corner to resize.

- `main.png`: initial form, including the existing live readout and selected time input.
- `records.png`: expanded recorded segments and project records, scrolled down.
- `invalid-duration.png` / `invalid-start.png`: field feedback and disabled Save.
- `long-project.png`: Persian/Latin custom project input with automatic text direction.
- `ProjectSelector-dropdown.png` / `CategorySelector-dropdown.png`: real native popup contents.

Both Save and Ctrl+Enter validate duration and start. Correcting one invalid field preserves the other error. Start edits are synchronous so a delayed event cannot replace later duration text. Oversized/non-finite parsing and arithmetic fail safely; positive adjustments cannot extend history before the supported date range. Existing duration formats and save/cancel/Undo/Delete/Discard semantics remain intact. Idle-stop retains its existing immediate preference-save behavior.

Verification uses `--timer-editor-native`, `--shortcuts-only` and `--unit-tests` through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1`. Synthetic settings/timers and private desktops keep tests off the active screen. Receipts are in `.superpowers/sdd/2026-11-09-timer-editor-native/`.

Limits: screen-reader announcements and physical mixed-DPI monitor transitions are not covered by these private-desktop checks.
Verified 2026-11-10: all nine focused editor groups and five input-hook checks passed, existing shortcut tests passed, and all 1,052 unit tests passed. Independent review findings were reproduced and corrected. Unit compilation reported NU1900 (vulnerability feed unavailable); no compilation errors.

Installed into `dist/desktop-windows` while the app was closed, with rollback in `dist/update-backups/timer-editor-20261110-123234`. Executable SHA-256: `8844781F5408992B8A0CEEF5079903854FEE6FD3495AC065545B323250A7A2FC`. The app was left closed.
