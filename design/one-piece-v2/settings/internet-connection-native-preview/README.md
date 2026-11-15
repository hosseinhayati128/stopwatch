# Internet & Network — Connection & Monitoring proposal

Native Avalonia design preview for review, not a production implementation. Uses the existing Settings shell, page scale, typography, and shared panel frame. All network names, measurements and timestamps are synthetic. No network probe or background monitor runs; no live settings, history, export files or installed binaries are changed.

## Layout

Two cards: **Last connection check** presents the result, network, latency, download speed, check time and Test now. **Background monitoring** presents automatic checking, the interval and the option to check only while a stopwatch runs. Cards sit side by side when space permits and stack at narrower content widths. The existing Done footer stays fixed. History & export is reserved as an inactive preview tab for the next layer.

This follows the upgraded integration pages. A single long settings form would mix a historical result with monitoring preferences; a dashboard with charts would expand this layer beyond the existing controls. The two-card arrangement is the proposed approach, awaiting visual approval.

## States and behavior for the proposed implementation

- Show Not checked yet until a result exists. Enabling monitoring is not evidence of an online connection.
- Online, Slow connection and Internet not reached describe the last check, not continuous live status. A timeout can also mean a blocked probe, so its explanation does not assert a disconnected adapter.
- Display measured latency when the download sample fails; label speed Unavailable instead of zero. Network details belong to the same check.
- Checking disables repeat Test now clicks. Manual testing remains available with automatic monitoring off, with no running stopwatch, or while correcting the schedule interval.
- Automatic checking and timer-only checking are independent preferences. Turning monitoring off preserves the displayed result. Timer-only mode can show Waiting for a stopwatch.
- Use an integer interval from 1–120 minutes, matching the existing scheduler's supported range; default 10. An invalid edit shows inline feedback and retains the last valid value. Existing scheduler startup timing remains a later implementation constraint: its first eligible check is after 30 seconds, then at the interval. The design does not invent a next-check countdown.
- Keep manual test persistence and automatic history/export behavior as currently implemented. History/export controls belong to the next layer.

## Native evidence

- `wide-70.png`, `wide-90.png`, `wide-125.png`: same 1120 × 720 native window at the requested app scales.
- Matching `-monitoring.png` captures show the lower portion when scrolling is needed.
- Compact 840 × 600 windows are also rendered at all three scales.
- Additional 90% examples: initial, checking, slow, unreachable, partial result, automatic checks off, waiting for a stopwatch, invalid interval and long network name.

The preview fixture checks horizontal bounds, metrics, keyboard focus, fixed footer, sample states and zero settings-change events. Run with the shared desktop runtime on a private desktop:

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-internet-preview -LogPath .superpowers/sdd/2026-11-15-settings-internet-preview/native.log
```

No real network connection or Linux rendering was tested. Production integration is pending design approval.
