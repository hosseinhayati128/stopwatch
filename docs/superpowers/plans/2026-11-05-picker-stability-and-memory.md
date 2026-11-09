# Standalone clock picker, overlay lifetime and artwork memory

User requirements: the Navigator picker must show only its own frame; repair the reported crash and make the application lighter. All native verification stays on a private desktop. Continue the current branch and preserve the unfinished Navigator work and unrelated Todo images.

Evidence: the terminating application log at 2026-11-05 21:13:04 reports `Cannot re-show a closed window` through `ToggleActiveTimerOverlay` and `ApplyTimerOverlayVisibility`. Both controller overlay caches currently retain closed windows. `StopwatchAppearanceAssets.Get` eagerly decodes all 40 source images (239.9 MiB of RGBA pixels), regardless of the selected frame.

1. Reproduce closed herd and separated overlays through real controller visibility actions. Clear cached references on Closed, handle teardown without mutating live dictionary enumeration, and verify repeated close/show and shutdown.
2. Verify the native picker has no OS caption/border and a transparent exterior. Apply native transparent/undecorated settings to Navigator, add header dragging, retain keyboard/pointer interaction, sizing and the legacy theme.
3. Measure cold first-artwork loading in a fresh native process; assert unrelated images remain unloaded. Make the existing shared image cache demand driven, preserving asset identity, drawing and all catalog validation. Compare startup time and decoded memory with the baseline; do not infer whole-system performance from this sample.
4. Run focused regression, existing Navigator/render/shortcut checks and the unit suite off screen. Review the final diff independently, publish a separate candidate, and update the stopped app after verification.

Interfaces: tasks 1 and 2 share no behavior. Task 3 changes asset initialization consumed by task 2 and overlays; existing catalog, pixel and native tests must remain green. No new artwork or rendered frame variants are needed.
