# Delete Time Record — native design proposal

A design-only Avalonia prototype for the confirmation opened by Delete in the Project Time Dashboard's Daily Timeline & Records. Uses the existing shared panel frame, parchment, Navigator heading and navy/muted-red actions. Logical size: 540 × 470.

## Layout

- Heading and vector X, followed by a short request to review the saved session.
- One record card: project name, separate Start and End columns, then a duration row. Both endpoints show their full dates, including the year, so crossing midnight does not hide which day is affected.
- Project names wrap within the same card. The overnight example also uses a longer project name.
- Plain consequence: permanently removes this session from reports; this cannot be undone.
- Fixed footer with Esc/cancel hint, navy Cancel and a separate pale Delete record action with muted-red lettering and border. Cancel receives initial focus in the prototype.

The compact card is the proposed approach, following the user's requested shared-frame direction. A larger stacked-action panel would add height to a single-record decision; a text-only confirmation would make it harder to verify the exact saved interval. The final appearance remains subject to visual approval.

## Preview matrix

All three cases are rendered at 70%, 90%, and 125% (378 × 329, 486 × 423, approximately 675 × 588 native client pixels in the capture environment):

| Case | Sample |
| --- | --- |
| Standard | Programming, 18 Nov 2026, 14:15–15:00, 45 min |
| Long name | Programming · Desktop interface improvements and accessibility review; same 45-minute interval |
| Overnight | Reading · Research notes and planning for tomorrow; 18 Nov 2026 at 23:30 to 19 Nov 2026 at 00:50, 1 h 20 min |

Individual PNGs are named `delete-time-record-{standard,long-name,overnight}-{70,90,125}.png`. Three `*-comparison.png` sheets place the corresponding captures side by side at their original pixel dimensions; the dark sheet background and scale labels are presentation aids outside the dialog.

## Prototype boundary

`tools/DesktopRegressionChecks/DeleteTimeRecordPreview.cs`, mode `--delete-time-record-preview`, creates throwaway native controls using synthetic dates and names. It does not instantiate the production dashboard or deletion dialog, read settings/history, or call deletion callbacks. Buttons have no action handlers. No new production artwork or dependencies.

Run through `tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1` only. The small layout checks cover client dimensions, text containment, footer separation, centered button labels and initial Cancel focus for every sample. Build/native logs and preservation hashes are retained locally in `.superpowers/sdd/2026-11-18-delete-time-record-preview/`.

Final build: zero warnings/errors. All nine native cases passed, and all three comparison sheets were visually inspected. The first 440-high draft overflowed the footer with two-line names; increasing the shared logical height to 470 resolved it at every requested scale. Transparent native window corners were also verified in the final captures. All windows stayed on the private desktop.

## Future implementation boundary

Keep the current record identity and dashboard deletion callback, active-record rejection, date conversion and UTC duration behavior. Preserve cancellation and focus-based keyboard activation, and make long/RTL names, larger typography and constrained displays reachable. The design shows the complete saved interval, including both days of an overnight entry; deletion must continue to target the original record rather than a clipped daily slice.

Actual deletion, persistence, OS keyboard delivery, legacy themes and physical monitor/DPI changes are outside this design spike. Production integration, installation and Git publication remain separate steps.
