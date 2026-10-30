# Nika Todo appearance implementation

> **For agentic workers:** Use superpowers:executing-plans to carry this approved design into the existing desktop application.

**Goal:** Implement the approved Classic and Pixel Voyage Todo overlay, all retained choices, supporting dialogs and immediate Settings preview.

**Architecture:** Keep task storage, interactions and synchronization in the existing Todo window. Add a versioned appearance model in Core, embedded artwork and reusable native visual helpers in Desktop, and a dedicated Settings partial. Apply the appearance only in Nika's Chronicle. A read-only sample preview must never use task storage or start sync.

**Tech Stack:** .NET 10, Avalonia 11.2.4, embedded PNG artwork, native controls.

**Spec:** `design/one-piece-v2/todo-overlay/design-selections.json`, README and approved PNG boards in that folder.

## Global Constraints

- Work on the existing `codex/nika-chronicle` feature checkout, preserving the untracked approved design files. No commit, push, deployment, startup changes or production app restart in this task.
- Target the Desktop project launched by publish.ps1. Keep other application themes and controller settings independent.
- Preserve hold completion, double-click progress, context menus, rich text, manual ordering, details, due times and sync semantics.
- Never run the live-data migration test or mutate real settings/Obsidian tasks in checks.
- Use native controls for all text and input. Artwork uses uniform scaling/cropping, never nonuniform stretching. Collapsed emblems have genuine alpha with no backplate.
- Save each profile independently. Keep all retained choices available in either profile. Header and launcher selection are independent; picker Cancel must not apply.

## Review Focus

Check non-Nika restoration, settings round trips including null/unknown fields, profile independence, new-task and detail-dialog scope, narrow-screen clipping, dark background contrast, preview side effects, selection versus apply in the emblem picker, and preservation of existing task interactions. Verify that atlas source rectangles do not include neighboring emblems.

### Task 1: Settings model and embedded assets

Files: Core/TodoAppearance.cs, Core/AppSettings.cs, Desktop/Assets/Todo, tools/DesktopRegressionChecks/TodoAppearanceChecks.cs.

- [x] Add a failing normalization/roundtrip test through the safe regression harness.
- [x] Add TodoAppearanceSettings with Classic and Pixel profiles; normalize retained IDs and defaults without losing the inactive profile.
- [x] Save the generated transparent emblem atlas and four background textures, including provenance and prompts.
- [x] Run the settings-only checks. Expected: defaults, malformed input and profile persistence pass.

### Task 2: Overlay native styling

Files: Desktop/Controls/TodoArtwork.cs, TodoVisuals.cs, TodoFrame.cs, Views/TodoOverlayWindow.*.

Interfaces: TodoAppearanceSettings.Current selects normalized TodoAppearanceProfile; TodoVisuals applies only to Nika; TodoArtwork renders Emblem/Background/Header by catalog ID.

- [x] Add regression checks for scoped styling and independent choices.
- [x] Implement joined header/body frame, four backgrounds, native checkbox and priority shapes, four rows, two detail layouts and two toolbars.
- [x] Preserve original controls and handlers; apply appearance after row construction. Restore defaults when leaving Nika.
- [x] Verify collapse/expand, all catalog choices and existing interactions with an isolated fixture.

### Task 3: Settings and preview

Files: Desktop/Views/SettingsWindow.axaml, SettingsWindow.TodoAppearance.cs, TodoEmblemPickerWindow.cs, Controls/TodoAppearancePreview.cs.

- [x] Add Todo appearance navigation, two profiles, background thumbnails and all retained selectors.
- [x] Add staged 16-emblem picker with Use/Cancel and independent header/launcher targets.
- [x] Build sample-only preview with the same native visual helpers; update immediately without reading user tasks.
- [x] Verify selection commits, profile retention and picker cancellation in regression checks.

### Task 4: Editors, menus and feedback

Files: Desktop/Views/NoteEntryWindow.*, ConfirmationDialogWindow.*, TodoOverlayWindow.Details.cs.

- [x] Apply Classic/Pixel frame and local palette only to Todo editors, detail editors and Todo confirmations.
- [x] Preserve save/retry text and backend errors; saved-with-warning has Close and Esc, no redundant Cancel or save hint.
- [x] Render due/progress/empty/completed/sync states consistently with the selected profile.
- [x] Verify normal Note and Reminder remain unchanged.

### Task 5: Verification and delivery

- [x] Build Desktop and regression harness with existing dependencies; avoid duplicate large runtime outputs when disk is low.
- [x] Run isolated Todo appearance checks plus existing Nika/controller checks and focused Todo behavior checks.
- [x] Capture and inspect native Classic/Pixel/settings/dialog screenshots at actual desktop sizes. Repair clipping or mismatched states.
- [x] Obtain one fresh review of the final diff; repair important findings with focused regression evidence.
- [x] Update the design manifest and README with implementation status and verification. Leave code ready for publish.ps1 without changing user data.
