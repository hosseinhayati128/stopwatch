# Review Projects Native Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Completed steps are checked below.

**Goal:** Implement the approved Projects & activity step of Activity & Time Review.

**Architecture:** Reuse the production window's controls, callbacks and core aggregation. Add a Nika-only presentation partial with the existing shared frame and form styles; retain the existing allocation/mood screens and other themes. Inject activity loading and settings persistence at their existing boundaries so native fixtures use synthetic data and memory callbacks.

**Tech Stack:** C# / .NET 10, Avalonia, existing native private-desktop regression harness.

**Spec:** design/one-piece-v2/activity-review/projects-native-preview/README.md and approved native captures.

## Global Constraints

- Preserve repeated selections, break segments, inline project registration, threshold grouping, retries, time allocation, mood and saving.
- Keep project/activity scrolling independent and sequence/actions fixed; bound long names and overflow.
- Use existing frame artwork and styles. No dependencies or runtime PNGs.
- All native execution uses Run-HiddenDesktop.ps1. No live settings/history writes or ActivityWatch requests in fixtures.
- Reuse current codex/stopwatch-overlay checkout. Preserve unrelated Todo PNG modifications. Deployment and publication are separate, user-authorized follow-up steps.

## Review Focus

- Chronological ordering must not make a remove button delete a different project; repeated and fixed entries need explicit mapping.
- Late or repeated activity responses must not overwrite selections or a later review step, or update a closed window.
- Keyboard Enter on a project/remove/group button must not also advance the wizard.
- Large lists, long/RTL names and short height must leave actions reachable.
- Nika changes must not alter the legacy appearance or allocation/save callbacks.

### Task 1: Production integration and native verification

**Files:** Modify PeriodicReviewWindow.axaml.cs and minimally its axaml for safe reparenting; create PeriodicReviewWindow.Navigator.cs. Add ReviewProjectsNativeChecks.cs and early drawing-only Program.cs route. Store real native captures under design/one-piece-v2/activity-review/native-implementation.

**Interfaces:** Existing PeriodicReviewWindow constructor and controller callers remain compatible. New optional loading/persistence delegates isolate the external boundaries. Existing aggregator outputs remain the source of activity percentages and allocation times.

- [x] Add a native regression asserting a framed window with fixed sequence/actions; run hidden and observe failure on the current layout.
- [x] Integrate the approved layout and actual project, sequence, activity and navigation handlers.
- [x] Verify repeated selection/removal, new project creation, thresholds/expansion, loading/offline/retry, cancellation and forward/back navigation using isolated native fixtures.
- [x] Verify large/RTL inputs, 520 DIP scrolling, alternate themes, and late activity completion.
- [x] Build, run focused native checks and the existing unit suite on private desktops; capture the actual production controls.
- [x] Perform one fresh final code review, fix material findings and repeat affected checks.
- [x] Report implementation and show native captures; leave installed app and unrelated artwork unchanged.
