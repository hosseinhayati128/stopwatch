# Nika's Chronicle Implementation Plan

> For agentic workers: use superpowers:subagent-driven-development. Keep changes on codex/nika-chronicle, in the current checkout. User approved the final controller design and explicitly requested implementation.

**Goal:** Add Nika's Chronicle as a second One Piece application theme with the approved controller appearance and independently persisted artwork, background and typography choices.

**Architecture:** Shared catalogs and settings in Core; separate WPF and Avalonia presentation integration. Reuse existing timer controls and commands. Embed artwork as separate backgrounds and decorative sprites, never as a screenshot containing controls. Per user correction, Nika inherits One Piece application resources and non-controller page layouts; its navy/parchment palette belongs only to the controller.

**Tech Stack:** .NET 10, WPF, Avalonia 11.2, XAML, xUnit.

**Spec:** design/one-piece-v2/design-decisions.json and approved-controller-concept.png. The user approved the final combined preview and all four interaction states. Original Map / Original Classic are initial defaults. Existing one piece remains distinct.

## Global Constraints
- Do not alter timer calculations, hotkeys, project data or existing theme identities.
- Preserve untracked design references. Work in the current checkout on a new feature branch; no publishing or merging.
- Application theme scope is the controller; every other page/layout retains the existing One Piece theme. Following floating clocks use One Piece; explicit independent selections remain respected.
- Shared contract: AppThemeCatalog.Nika = "Nika's Chronicle". AppSettings string properties NikaBackground = "original-map", NikaTypography = "original-classic", NikaSidebarArtwork = "sun-warrior".
- NikaThemeCatalog.Backgrounds, Typography, Artworks expose IReadOnlyList<NikaThemeOption> (Id, Name). NormalizeBackground/NormalizeTypography/NormalizeArtwork return valid ids with defaults. All settings normalize on load/save.
- Asset relative names: Assets/Nika/map.png, warrior.png, log-pose.png, mural-atlas.png. Parent prepares them in both frontends. Atlas cells may be cropped at rendering time, excluding labels, via catalog metadata.
- Alternate choices: backgrounds original-map/amber-atlas; typography original-classic/typewriter-log/chartmakers-script/navigator-precision; murals sun-warrior plus the 16 numbered saved concepts.

## Review Focus
- Switching away restores normal layout and typography; previous one piece still loads.
- Malformed/missing options normalize safely and round-trip independently of overlay settings.
- Theme remains usable at narrow widths and increased UI scale; decorative artwork never intercepts input or hides timer list.
- Idle/paused/running, hover and focus remain distinguishable and keyboard-operable.
- Embedded assets resolve in Release builds; no reliance on .codex generated-image paths or Downloads.

## Task 1 — Shared settings and assets (parent)
- [x] Add normalization/persistence tests in StopwatchOverlay.Tests/NikaThemeSettingsTests.cs; focused Release tests pass. Initial red run was blocked by sandbox test-host IPC; no successful red run claimed.
- [x] Add catalog and AppSettings options in Core; add new application theme without changing Pirate. Define intentional Follow Application overlay behavior using an existing palette.
- [x] Generate clean parchment, default mural and Log Pose assets; retain the selected 16-mural sheet as an embedded atlas with explicit source rectangles.
- [x] Run shared tests including existing normalization and overlay-theme combinations.

## Task 2 — WPF presentation (WPF implementer)
- [x] Add Nika palette and narrowly scoped controller styling/layout, decorative elements, live state indicator and typography choice application.
- [x] Expose background, typography and sidebar artwork selectors in Settings; update controller immediately on changes and save through existing pipeline.
- [x] Check prior theme switching and add meaningful rendering/resource regression checks. Build Release and render controller in isolated preview settings.

## Task 3 — Avalonia presentation (Desktop implementer)
- [x] Add matching Nika palette, scoped controller styles/decorations and independent typography choices.
- [x] Add settings controls wired through existing save/change pipeline; handle live switching and bounds.
- [x] Build Release and extend existing headless checks where useful. No WPF/Core edits by this implementer.

## Task 4 — Integrate and verify (parent/reviewer)
- [x] Inspect rendered screenshots, fix layout and interaction gaps, run solution Release tests and desktop regression checks appropriate to touched surfaces.
- [x] Independent reviewer checks feature boundaries, persistence, switching, resource loading and visual gaps. Resolve findings.
- [x] Document implemented choices and exact validation evidence. Leave changes reviewable on feature branch; do not deploy or push.

## Coordination ledger
Preflight: task 1 supplies the catalog/settings and asset paths consumed by tasks 2/3; names above are binding. Tasks 2 and 3 touch disjoint frontend directories. Parent owns Core, tests for shared settings, assets, and integration documentation. No conflicting file ownership.
Ruling: use existing checkout and new feature branch rather than copying untracked approved artwork into a separate worktree. The original source is clean; only current design references were untracked.
Ruling: exact generated fonts are visual concepts; use bundled/openly licensed real fonts and inspect output before claiming fidelity.

Validation evidence: design/one-piece-v2/implementation-validation.md. Windows 999/999 pass; both frontends build and Nika checks pass. Broader Desktop suite has one isolated task-list failure outside changed production sources, documented without claiming baseline proof.
