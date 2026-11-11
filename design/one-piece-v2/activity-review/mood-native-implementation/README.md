# Feelings & Mood — native implementation

Actual `PeriodicReviewWindow` controls, captured on a private desktop with synthetic review data. The installed application and user records were not changed.

The final review step uses the shared Navigator frame and existing score, feeling, note and save handlers. Its time summary includes recorded and newly allocated project time, with breaks and gaps shown separately. Selected feeling buttons retain keyboard focus. Score presets, custom feelings and Add handle Enter without also saving the review; the optional note remains multiline and supports Persian/RTL text.

## Captures

- `neutral.png`: configured neutral score and empty optional fields.
- `selected-feelings.png`: score 7.6, three selected feelings and a reflection.
- `low-mood.png`: low score and related feelings.
- `custom-feeling-rtl.png`: Persian custom feeling, pending input and reflection.
- `short-window.png`: independently scrolled panes and fixed actions at 850 × 520 DIP.
- `many-feelings.png`: many custom feelings at the minimum window size, scrolled to the reflection.

## Verification

Build `tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj`, then use `Run-HiddenDesktop.ps1 -Mode '--review-mood-native'`. The fixture loads production controls/styles without the live app, settings persistence, global hooks or tray. Six groups cover normal and short layout, score defaults/limits/precision/presets, keyboard/duplicate/long/RTL feelings, navigation and live summaries, save/time-only/cancel payloads, legacy themes and native captures.

The existing allocation and project suites and focused `MoodTests` / `PeriodicReviewTests` provide adjacent coverage. No new artwork, runtime dependency or application timer was added. Installation and Git publication remain separate steps.
