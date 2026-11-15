# Habitica Connection & Status Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans in this session. Follow the checkbox steps.

**Goal:** Integrate the approved Habitica Connection & Status native design while preserving existing sync/recovery actions.
**Architecture:** Keep HabiticaSettingsPanel as the behavior owner. Add its navigator-layout partial, reparent original controls, and use a small SettingsWindow wrapper for the shared page scroll. Existing HabiticaIntegration and its injected API/secret store provide the real action boundary.
**Tech Stack:** C#, Avalonia, existing private desktop harness.
**Spec:** design/one-piece-v2/settings/habitica-connection-native-preview/README.md

## Global Constraints
- Every UI check uses a private Windows desktop. Sample vault/state and in-memory HTTP/secret store only; never contact live Habitica, write real credentials, or read the real clipboard.
- Keep existing explicit Save/Test, blank-keeps-token, token clearing, installation ownership, rate limiting and recovery confirmations.
- Preserve current dirty Telegram work and shared frame/clock artwork. No installed-app update, commit or push.
- Show actual production native captures at completion.

## Review Focus
- Credential storage feedback must belong to the displayed account, and Show must reveal only the draft.
- Account edits, navigation, close and theme changes must mask drafts and prevent stale test feedback or duplicate requests.
- Save failures must preserve the previous committed account; missing tokens, invalid IDs, session-only storage and request delays must remain understandable.
- Existing sync/recovery controls and confirmations must remain reachable through theme/layout changes.
- Compact widths and enlarged text must preserve focus, field fit and the fixed footer without adding background work while the page is hidden.

### Task 1: Integrate Habitica Connection & Status
**Files:** Controls/HabiticaSettingsPanel.cs and new .Navigator.cs/.Connection.cs, new Views/SettingsWindow.Habitica.cs, SettingsWindow.Navigator.cs, SettingsWindow.axaml.cs, shared persistence footer in SettingsWindow.TodoAppearance.cs, native Habitica fixture and Program/shell/archived-preview adaptations; implementation captures/docs.
**Interfaces:** Keep HabiticaSettingsPanel.Bind/Reload, existing injected HabiticaIntegration/clipboard dependencies, HabiticaApiClient and IHabiticaSecretStore. Add SetNavigatorLayout(bool), HideConnectionDraft(), page Build/Restore hooks. Original controls and handlers must remain single instances.

- [x] Write native checks for draft reveal/save/blank retention, validation/callbacks, real test outcomes using fake HTTP, session-only credentials, duplicate and stale tests, legacy theme restoration, hidden-page behavior, retained advanced actions and layout captures.
- [x] Build fixture and run privately against old UI. Expected: missing navigator/reveal behavior; no real service calls.
- [x] Integrate responsive layout and status presentation, preserving original advanced controls in an accessible Sync & recovery section. Restore legacy layout/styles on other themes.
- [x] Connect accurate feedback and busy/delay/draft lifecycle, snapshot async settings, and preserve existing service validation and storage semantics.
- [x] Build and run native fixture. Expected: all cases pass at 70/90/125 percent and enlarged text, with native captures.
- [x] Run adjacent Settings/Telegram/Obsidian, existing Habitica coverage and full unit suite privately. Expected: no regressions; carryover file hashes remain unchanged outside declared integration files.
- [x] Obtain one fresh read-only final review; fix consequential findings with RED/GREEN checks and report verified results.
