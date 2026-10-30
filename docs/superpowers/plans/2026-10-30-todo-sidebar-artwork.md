# Todo sidebar artwork layer implementation plan

> **For agentic workers:** Use superpowers:executing-plans in the current checkout. Preserve the existing uncommitted Todo implementation.

**Goal:** Let the user place any existing sidebar artwork over the Todo map as an optional, translucent background layer.

**Architecture:** Add independent artwork/opacity preferences to each Todo profile. A decorative native control reuses the existing Nika assets and crop catalog, removes the atlas's navy backing at render preparation, and fits the motif uniformly beneath task controls. Settings and their preview use the same renderer.

**Tech stack:** .NET 10, Avalonia, existing embedded images and native regression runner; no added dependencies or generated assets.

**Spec:** The user's request in this chat to use sidebar artworks as a Todo overlay background layer, plus established preferences for independent Classic/Pixel settings and preserving the approved layout. Implementation choices: default None for compatibility; 18% initial opacity, adjustable 0–100%; centered in the body using visible artwork bounds (per the user's alignment correction); warm sepia on parchment and gold on Midnight Chart. No new questions are needed for this optional extension.

## Global constraints

- All 17 sidebar artworks plus None, independent of the controller, header and launcher.
- Preserve existing maps, controls, borders, native text, drag handling and transparent collapsed launcher.
- Never stretch the artwork, include atlas captions, or show its navy rectangle on parchment.
- Inactive themes hide the layer. Settings apply immediately and remain separate per profile.
- Use isolated fixtures; do not publish, restart the user's app, migrate real data or commit unrelated dirty work.

## Review focus

- Old/invalid settings must leave the layer disabled; malformed opacity must normalize safely.
- Every atlas crop must contain a visible transparent motif without captions or rectangular backing.
- Switching profiles, maps or theme must update or hide the layer and preserve independent choices.
- Opacity dragging must retain its control/capture while updating the preview and overlay.
- The layer must not intercept completion, manual dragging, scrolling or collapse input.

## Task 1: Optional artwork throughout Todo appearance

**Files:** Core/TodoAppearance.cs; Desktop Controls/TodoMuralArtwork.cs and TodoAppearancePreview.cs; Views/SettingsWindow.TodoAppearance.cs, TodoOverlayWindow.axaml and TodoOverlayWindow.Appearance.cs; regression runner and checks; README.

**Interfaces:** `TodoAppearanceProfile.SidebarArtwork` (default `none`), `SidebarArtworkOpacity` (default `.18`); catalog `SidebarArtworks`; `TodoMuralArtwork.Apply(TodoAppearanceProfile)` sets choice, tint, opacity and visibility on a noninteractive control.

- [x] Add compatibility/persistence coverage and observe missing-preference failure.
- [x] Implement normalized settings, transparent artwork rendering, optional live layer and settings preview/controls.
- [x] Verify all motifs, native settings selection and opacity updates, profiles, theme restoration and launcher isolation in fixture windows; inspect native screenshots.
- [x] Build and run focused appearance and reordering regression checks, then obtain one fresh review of this change and record results.

## Verification

Final regression-project build: zero warnings and errors. `--todo-mural-only`, `--todo-appearance-only`, and `--todo-reorder-only` all pass using isolated fixtures and the shared Desktop runtime. `git diff --check` passes. Native screenshots were inspected for Classic, Pixel, Midnight Chart and compact Settings, and are stored under `design/one-piece-v2/todo-overlay/implemented/mural-*.png`.

The opacity regression initially measured a 0.121 alpha ratio at 50% because nested Avalonia masks compounded it. Preparing transparency, tint and edge fade in the cached image removed the masks; the regression now measures 0.500. No source images were changed.

A fresh read-only review approved the scoped change with no actionable findings. The reviewer did not exercise OS input or run tests; parent verification covers actual windows, routed native interaction in the existing drag suite, and settings/renderer checks. Physical mouse end-to-end testing is not claimed. Previous appearance and drag implementation remains outside this change's scope. No production settings/task edits, app restart, publishing or Git publication occurred.
