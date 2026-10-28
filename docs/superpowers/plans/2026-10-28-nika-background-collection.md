# Nika Background Collection Implementation Plan

> **For agentic workers:** Execute this small approved change inline using superpowers:executing-plans and verification-before-completion.

**Goal:** Add all ten approved maps, refined with faint left and bottom detail, as independent Nika background choices.

**Architecture:** Extend the shared catalog and map each new stable ID to an embedded PNG. Avalonia and WPF consume the same mapping. Retain the two existing backgrounds and the user's selected settings.

**Tech Stack:** C#, Avalonia, WPF, embedded PNG resources.

**Spec:** User approval of all ten options in `design/one-piece-v2/background-options-2026-10-27/manifest.json`, followed by the request for sparse, faded artwork along the left and bottom.

## Constraints and review focus

- Keep the existing theme, typography, sidebar artwork and timer behavior independent.
- Keep current background defaults and saved IDs valid; expose twelve total choices.
- Use the exact refined PNGs, with uniform scaling to preserve round compasses.
- Verify persistence, selection, distinct rendered backgrounds, and embedded assets in both apps.
- Continue in the existing dirty feature checkout; do not commit or replace the running application.

## Task 1: Add the refined collection

- [x] Preserve original and refined artwork with prompts and update the comparison gallery.
- [x] Extend existing settings tests to cover persistence of all ten new IDs and twelve choices; run before implementation.
- [x] Add `NikaThemeCatalog.GetBackgroundAsset(string? background)` and the ten named options; copy refined PNGs to both apps.
- [x] Use the shared asset mapping in Avalonia `NikaArtwork` and WPF `NikaVisual`, preserving aspect ratio and original background treatment.
- [x] Exercise each selector and render all twelve backgrounds; verify they are distinct and inspect actual controller samples.
- [x] Run focused settings tests and Desktop Nika checks, then publish the staged Windows package.

## Progress

- All ten artwork edits generated with built-in imagegen; left and lower detail inspected.
- Ruling: retain the existing working tree because it contains the accepted controller work. No worktree copy or unrelated cleanup is needed.

- Completed: 14 settings tests, 12 distinct background renders, Desktop layout/mode checks, independent review, and staged Windows publish all passed. Package: dist/desktop-windows-ready/StopwatchOverlay.Desktop.exe (109468942 bytes).
