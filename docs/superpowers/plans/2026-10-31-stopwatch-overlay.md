# Stopwatch Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved layered stopwatch appearance, all retained options and its two-tab inspector in the existing Avalonia desktop application.

**Architecture:** Add one versioned Core appearance profile and a scoped Nika overlay theme. Keep the existing overlay window, controller, events and native window behavior; compose new visual layers through the shared clock renderer and a toolbar styler. Prepare the default assets first, prove the default in a native window, then complete the catalogs and inspector.

**Tech Stack:** C#, .NET 10, Avalonia 11.2.4, existing Skia/SVG support, bundled raster/vector assets and licensed fonts; xUnit for Core and `tools/DesktopRegressionChecks` for actual Desktop UI. No package upgrades or new runtime dependencies are planned.

**Spec:** [Stopwatch overlay design](../specs/2026-10-31-stopwatch-overlay-design.md). Read it and [the exploration manifest](../../../design/one-piece-v2/stopwatch-overlay/design-exploration.json) before execution. Builder check found no important open questions; native rendering and fonts remain implementation work.

## Global Constraints

- Target `StopwatchOverlay.Desktop` (Avalonia), with settings and pure policies in Core. Do not implement a second renderer in the legacy WPF application.
- Retain 12 backgrounds, 16 frames, 10 typography profiles, 3 artwork treatments and 12 hover-control styles as independent selectors.
- Default IDs: `navigator-parchment`, `engraved-filigree`, `navigator-bold`, `clean`, `filigree-keys`. State: Project Markers revision 2, no underline.
- New profile defaults: Background 70% (0–100), Unselected 40% (10–100). Selected is 100%; whole-clock dimming follows selection, not running.
- Time size stays shared at existing 16–120, default 48; saved choices survive. No per-timer appearance profiles.
- Keep all four timing modes, combined/separate routing, drag, shortcuts, click-through, capture hiding, topmost/no-activate and existing close/pause/reset behavior.
- Keep the toolbar's Close, Pause/Resume, Reset order and 200 ms departure delay; Edit and Separate/Merge remain hidden.
- Production assets contain no baked time, names, board labels or desktop backdrop. Use the original Luffy emblem, not generated emblem artwork.
- Preserve legacy settings, independent application/overlay themes, Todo and unrelated UI. Do not write real user settings in tests; use explicit fixture paths and verify isolation.
- Use the approved Style/Transparency inspector with immediate apply and Done-to-close. Preview Running/Paused and Light/Dark controls do not affect real timers.
- No implementation was performed while writing this plan. Commands below are execution instructions, not reported test results. Reuse the suitable checkout unless isolation is needed; preserve unrelated work. Commit/publication is outside this plan unless separately authorized.

## Review Focus

1. Older, partial, corrupt or future-ID settings: preserve valid saved values and normalize only the affected new fields; prove missing versus explicit legacy opacity and 0% handling (Task 1).
2. Long/RTL/empty names and large time strings at 16/48/120: stable tick width, legible live glyphs and fixed state geometry, including REC (Tasks 2 and 4).
3. Native Popup ownership: dim toolbar exactly once, maintain pointer crossing and click-through behavior, and avoid stealing focus during live state changes (Task 3).
4. Independently mixed styles: all 192 background/frame pairs remain valid; frames are not removed because their matching toolbar was rejected; textures and ornaments keep correct clipping/aspect (Task 4).
5. Settings at minimum size and high DPI: controls remain reachable, preview matches live rendering, toggles do not issue timer commands, and save failures are visible without discarding session changes (Task 5).

## File and interface map

Paths below are repository-relative. New files are explicitly identified in tasks. Existing `Assets/**` are already included as Avalonia resources by the Desktop project.

| Area | Files / responsibility |
| --- | --- |
| Core model | New `StopwatchOverlay.Core/StopwatchAppearance.cs`: profile, immutable option metadata, logical flags |
| Core policies | New `StopwatchAppearancePolicy.cs` and `StopwatchAppearanceMigration.cs`: alpha/marker selection and one-time JSON migration |
| Persistence/routing | Existing `AppSettings.cs`, `OverlayThemeCatalog.cs`; existing Desktop `ThemeResources.cs` plus new `Themes/Overlay/NikaOverlay.axaml` |
| Resources | New `StopwatchOverlay.Desktop/Assets/Stopwatch/asset-manifest.json`, `Backgrounds/`, `Frames/`, `Controls/`, `Thumbnails/`; licensed fonts under `Assets/Fonts/Stopwatch/` only when needed |
| Clock rendering | New Desktop `Controls/StopwatchAppearanceRenderer.cs`, `StopwatchAppearanceAssets.cs`, `StopwatchTypography.cs`; integrate through existing `ThemedClockFace.cs` |
| Hover rendering | New Desktop `Controls/StopwatchToolbarStyler.cs`; style existing Buttons and popup content, do not replace their behavior |
| Live integration | Existing `Views/OverlayWindow.axaml[.cs]`, `Views/ControllerWindow.axaml.cs` |
| Inspector | New `Views/SettingsWindow.StopwatchAppearance.cs`, `Controls/StopwatchAppearancePreview.cs`; small host wiring in existing `SettingsWindow.axaml[.cs]` |
| Tests/evidence | New Core-focused xUnit files and new `tools/DesktopRegressionChecks/StopwatchAppearanceChecks.cs`, `StopwatchAppearanceUiChecks.cs`; add focused command flags to existing `Program.cs`; visual evidence in `design/one-piece-v2/stopwatch-overlay/implementation-review/` |

## Task 1: Persist the independent appearance profile and pure policies

**Files:** Create the three Core files in the map; modify `StopwatchOverlay.Core/AppSettings.cs`; create `StopwatchOverlay.Tests/StopwatchAppearanceSettingsTests.cs` and `StopwatchAppearancePolicyTests.cs`. Theme routing itself lands with native support in Task 3.

**Interfaces (new, in namespace `StopwatchOverlay`):**

- `StopwatchAppearanceSettings`: `int Version = 1`; string properties `Background`, `Frame`, `Typography`, `Artwork`, `HoverControls` with approved default IDs; double `BackgroundOpacity = 70`, `UnselectedOpacity = 40`; `StopwatchOpaqueLayers OpaqueLayers = Default`; methods `void Normalize()` and `StopwatchAppearanceSettings Copy()`.
- `[Flags] StopwatchOpaqueLayers`: `None=0`, `Frame=1`, `TimeText=2`, `ProjectName=4`, `StateMarkers=8`, `HoverControls=16`, `Artwork=32`, `Default=31`, `All=63`. Background is not an opaque group. Default Artwork is unchecked; Clean only disables its UI.
- `record StopwatchAppearanceOption(string Id, string Label)`; `StopwatchAppearanceCatalog.Backgrounds/Frames/Typography/Artworks/HoverControls` each `IReadOnlyList<StopwatchAppearanceOption>` with exactly the spec IDs. `string NormalizeId(string? value, IReadOnlyList<StopwatchAppearanceOption> choices, string fallback)` trims and compares ordinal-ignore-case; returns canonical ID.
- `AppSettings.StopwatchAppearance` is a non-null profile initialized to `new()`, with null repair in `NormalizeForRuntime()` before calling its normalization. JSON name stays `StopwatchAppearance`.
- Internal `StopwatchAppearanceMigration.Apply(AppSettings settings, JsonElement root)`: if profile object is absent/null, migrate finite numeric legacy opacity properties that are actually present. Otherwise leave the new profile authoritative. Call in `SettingsStore`'s successful JSON load path before normalization; do not change its backup/corruption handling.
- `enum StopwatchMarker { Running, Paused }`; `StopwatchAppearancePolicy.MarkerFor(bool isRunning, bool isClockMode)` returns Running when either is true. `LayerOpacity(StopwatchAppearanceSettings profile, StopwatchOpaqueLayers layer)` and `SelectionOpacity(StopwatchAppearanceSettings profile, bool isSelected)` return normalized 0–1 multipliers. LayerOpacity handles one logical group; background uses profile.BackgroundOpacity/100 directly.

- [ ] Add failing settings tests with exact assertions: new profile IDs match the spec; counts equal 12/16/10/3/12; IDs unique; retained IDs match the checked-in manifest; unknown `Frame` falls back to `engraved-filigree` while a valid `Background="midnight-chart"` survives; `" CLEAN "` normalizes to `clean`; artwork preference survives Clean→Corner Stamp→Clean.
- [ ] Add failing load/round-trip fixtures using `SettingsStore.Load(path)` and `Save(settings,path)`: `{}`→70/40, explicit legacy 0/35→0/35, 85/55→85/55, only legacy background20→20/40, existing profile25/60 plus legacy85/55→25/60. Assert legacy fields, TextSize, ThemeMode, OverlayTheme and a nondefault Todo choice survive save/load. Test a partial profile with only Frame, null profile, unknown IDs, in-memory NaN/infinity, out-of-range opacity and unknown flags. Normalize known schema to Version1, finite values to ranges, nonfinite to70/40, unknown flag bits away. Malformed JSON keeps existing SettingsStore recovery behavior.
- [ ] Add failing policy assertions: selected/unselected multipliers1/.4 independent of running; checked Frame1, unchecked Artwork.7, background.7; resulting background.28 and frame.4 when unselected; 0 background stays0; MarkerFor(false,true)=Running and MarkerFor(false,false)=Paused. Do not expand legacy `NavigatorOpaqueParts`.
- [ ] Run `dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj --filter "FullyQualifiedName~StopwatchAppearance" -m:1`. Confirm failure is due to the absent new interfaces/assertions, not unrelated environment problems.
- [ ] Implement the interfaces and one-time migration. Keep options free of arbitrary file paths; resource URIs will be resolved from whitelisted IDs. Preserve default text size and all legacy opacity fields. Normalize without mutating catalog metadata.
- [ ] Rerun the focused command; expect all new settings/policy tests to pass. Review the diff for unrelated changes before proceeding.

## Task 2: Prepare and render the approved default with live text

**Files:** Create default assets and manifest, `StopwatchAppearanceAssets.cs`, `StopwatchTypography.cs`, `StopwatchAppearanceRenderer.cs`; modify `ThemedClockFace.cs`; create `tools/DesktopRegressionChecks/StopwatchAppearanceChecks.cs` and add a focused `--stopwatch-renderer-only` route in `Program.cs` after Avalonia initialization. Keep this slice exercisable in fixtures before routing real themes to it.

**Interfaces (new, Desktop Controls unless stated):**

- `record StopwatchPresentationState(bool IsActive, bool IsRunning, bool IsClockMode)`; this is visual state and never owns a timer.
- `StopwatchAppearanceRenderer(StopwatchAppearanceAssets assets)`; `Size Measure(ThemedClockFace face, StopwatchAppearanceSettings profile, StopwatchPresentationState state)`; `void Render(DrawingContext context, Rect bounds, ThemedClockFace face, StopwatchAppearanceSettings profile, StopwatchPresentationState state)`. Input face supplies live Text, ProjectName, TextSize, ShowRec and configured brushes.
- `ThemedClockFace.ApplyStopwatchAppearance(StopwatchAppearanceSettings? profile)` copies normalized input; null restores the existing renderer. `SetStopwatchPresentationState(StopwatchPresentationState state)` invalidates visuals, not timer values. Its existing MeasureOverride/Render delegate only when the copied profile is set. Keep the current automation name with full project/time text.
- `StopwatchAppearanceAssets` owns cached resource metadata/bitmaps and validated frame metrics. `StopwatchTypography.Resolve(string id)` returns `StopwatchTypographyProfile`; that profile records exact timer/name FontFamily, weight, digit-renderer kind and fixed numeral advance policy. Definitions are immutable and reusable.
- Runtime asset manifest fields: `schemaVersion`, `assets[]` with `kind`, `id`, `path` or declared `proceduralRenderer`, `sourceBoard`, `sourceOption`, `licenseOrProvenance`, `sourceSize`, `contentInsets`, `sliceInsets`, `minimumSize`, `intrinsicOpacity`; typography entries record actual family/file/license and any custom glyph renderer. Nonapplicable geometry fields may be omitted. No pending/placeholder entries in a finished catalog.
- Native test entry points: `internal static void StopwatchAppearanceChecks.Renderer()` and later `CatalogAssets()`; export fixtures to the implementation-review directory. Add methods as their tasks land, not placeholder passing checks.

- [ ] Audit the actual default references and existing assets. Prepare Navigator Parchment, Engraved Filigree and Filigree Keys as separate text-free resources. Use imagegen for missing raster art after inspecting the local references. Save exact prompts/provenance; verify transparent backgrounds and ornate shapes visually. Reuse the original emblem only when later artwork layers need it.
- [ ] Record actual default fonts and measurements after examining live candidates: try bundled Barlow Condensed SemiBold for digits and Almendra Bold for project name first, compare to Navigator Bold, and adjust the builder-owned mapping if needed. Verify loaded family names and licenses; do not silently render a system fallback.
- [ ] Add failing native renderer checks: default assets decode; repeated times `00:00:00.0`/`11:11:11.1`/`88:88:88.8` have equal layout width; selected-running and selected-paused bounds match; empty/long/Persian names fit reserved area and preserve accessible text; 123-hour strings do not clip. ACTIVE adds no height/position change; no underline visual exists. Test 16/48/120 sizes and REC enabled.
- [ ] Build/run `dotnet run --project tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj -- --stopwatch-renderer-only`; expect intended new checks to fail before implementation. The new flag should use the existing `Check`/failure-count exit code pattern.
- [ ] Implement asset caching, default typography and measure/render delegation. Render background+mask, optional artwork, frame, time/name, fixed marker slot, REC and ACTIVE tab with stable geometry. Keep frame metrics independent of selection. Background uses profile alpha; each logical group uses Task1 LayerOpacity. Do not apply whole-window selection opacity inside this clock renderer; its host owns that multiplier.
- [ ] Rerun the focused native command; expect exit0 and all checks PASS. Inspect light/dark default renders against the reference board at actual size. Capture this as the first reviewable appearance milestone, explicitly labeling generated references versus native renders.

## Task 3: Wire the default into live windows and hover controls

**Files:** Modify `StopwatchOverlay.Core/OverlayThemeCatalog.cs`, Desktop `ThemeResources.cs`, `ThemedClockFace.cs`, `Views/OverlayWindow.axaml[.cs]`, `Views/ControllerWindow.axaml.cs`; create `Themes/Overlay/NikaOverlay.axaml` and `Controls/StopwatchToolbarStyler.cs`. Extend `OverlayThemeSettingsTests.cs`, `StopwatchAppearanceChecks.cs`, `Program.cs`; update existing Nika checks only where the new explicit overlay routing intentionally changes expectations.

**Interfaces:**

- New `OverlayThemeCatalog.Nika = AppThemeCatalog.Nika`; add to All/Normalize; Resolve(FollowApplicationTheme,Nika)=Nika; Resolve(Pirate,Nika)=Pirate; Resolve(Nika,Midnight)=Nika. `ThemeResources.OverlayFile(Nika)` maps to `NikaOverlay`; include required shared tokens without replacing application resources.
- New `OverlayWindow.ApplyAppearance(AppSettings settings)` centralizes existing theme + legacy ApplySettings calls, then supplies the copied stopwatch profile only for effective Nika. Retain existing public methods for callers/tests; ControllerWindow.ApplyOverlayStyle delegates to this method.
- New `StopwatchToolbarStyler.Apply(Control toolbarRoot, IReadOnlyList<Button> actionButtons, StopwatchAppearanceSettings? profile, StopwatchPresentationState state, double selectionOpacity)`: non-null selects style and applies logical layer/selection opacity once; null restores saved legacy templates and local values. Buttons remain existing Close/PauseResume/Reset objects; do not add handlers on every Apply. Styler derives pause/play action icon from running and clock mode while the window retains IsEnabled.
- New `StopwatchAppearancePolicy.ResolveInactiveOpacityPercent(AppSettings settings)` selects profile UnselectedOpacity for effective Nika, otherwise the existing normalized legacy value. Use it for both herd and separate paths in UpdateOverlayStates.
- New native check `StopwatchAppearanceChecks.LiveOverlay()`; `--stopwatch-appearance-only` runs settings-equivalent assertions as needed, Renderer and LiveOverlay, later CatalogAssets/inspector checks as implemented.

- [ ] Add failing routing tests covering independent Nika selection, Follow→Nika, explicit Pirate retention, all existing theme combinations and unknown fallback. Add native theme-switch test Nika→Pirate→Nika: default profile returns unchanged; original toolbar templates/icons restore and no duplicate event subscriptions remain.
- [ ] Add failing native state tests using isolated timer/session fixtures: selected paused remains100% with ACTIVE; unselected running is40% without ACTIVE; clock mode shows running marker with disabled pause; reset state shows pause cue. Test herd representing an inactive session alongside a selected separate timer; switching active sessions updates both without changing elapsed time or position.
- [ ] Add popup tests with a shown native overlay: controlled pixels prove background .70/.28 and checked toolbar .40 on an unselected clock; unchecked toolbar .70×.40=.28, never .16 from double dimming. Test background0 preserves checked text. Cover keep-opaque flags, intrinsic transparent edges and popup screen-root propagation explicitly.
- [ ] Run the focused Core tests and new `--stopwatch-appearance-only` route; confirm failures identify missing routing/behavior.
- [ ] Implement Nika resources/routing and forwarding of SetActive, SetRunning and SetPauseResumeEnabled into presentation state. Preserve the existing border selection indicator for legacy themes and suppress it for Nika. Use SetPauseResumeEnabled to convey clock-mode state without inventing timer transitions. Cache and reapply popup styles/state on open. Root/window selected multiplier is applied once; popup content receives the effective multiplier once according to its actual native root behavior.
- [ ] Implement Filigree Keys on existing Buttons, preserving automation, tooltips, hit regions and input handlers. Add no style-based action dispatch. Keep native topmost/no-activate/capture hooks, hidden actions, hover timers and drag/right-click logic intact.
- [ ] Run native checks for pointer crossing (clock→toolbar stays open), >200ms departure closure, click-through closure/suppression, one click→one existing event, right-click toggle, drag and hide/show. Use an explicit isolated fixture; do not operate the user's running stopwatch. Confirm no native window steals foreground focus on state updates.
- [ ] Run `dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj --filter "FullyQualifiedName~StopwatchAppearance|FullyQualifiedName~OverlayThemeSettings|FullyQualifiedName~NavigatorTransparency" -m:1` and the focused native route; expect exit0. Inspect the default live overlay before expanding the library.

## Task 4: Complete every retained asset, typography and toolbar style

**Files:** Extend `Assets/Stopwatch/`, `StopwatchAppearanceAssets.cs`, `StopwatchTypography.cs`, `StopwatchAppearanceRenderer.cs`, `StopwatchToolbarStyler.cs`, and `StopwatchAppearanceChecks.cs`. Add licensed files under `Assets/Fonts/Stopwatch/` only if bundled candidates cannot match approved styles. No additional settings fields or motifs.

**Interfaces:** Use the immutable catalogs from Task1 and rendering contracts from Tasks2–3. `StopwatchAppearanceChecks.CatalogAssets()` covers all entries and resource metadata. Resource lookup by retained ID must return a definition; a missing production asset is a failing check, not a generic default silently substituted.

- [ ] Inspect all retained boards and produce a coverage manifest joining each retained ID to a real asset/procedural implementation. Keep original 14 frames plus15/16, and only the12 retained controls. Generate or prepare the remaining 11 backgrounds, 15 frames, 11 control styles and two original-emblem treatments as needed after reusing matching existing assets.
- [ ] Add failing coverage checks: every retained ID resolves to loaded assets or its declared live procedural renderer; alpha textures retain transparent margins; corner/rail insets are within source dimensions; selector thumbnails use the same definitions; no path comes directly from user JSON. Compare catalogs to the exploration manifest, including retained Rope Binding, Chart Scroll, Admiralty and Bound Logbook frames.
- [ ] Complete exact licensed typography mappings for all10 profiles. Use live seven-segment geometry for Instrument Digital and live pixel/vector/outline glyphs where fonts do not reproduce the design. Preserve accessible text and support all existing formatted time characters. Verify Typewriter Log's zero against corrected v2 board; do not accept a crossbar that recreates the rejected leading-zero appearance.
- [ ] Add glyph tests for each style: all digits0–9 and punctuation used by existing timer formatter; fixed numeric advance; safe display at16/48/120; hours beyond99; names with Persian shaping, mixed Latin/digits, empty and long text. Compare actual runtime-loaded fonts to declared mappings. Font fallback for unsupported name scripts is allowed only if text remains shaped and readable; record the chain.
- [ ] Run `--stopwatch-appearance-only`; confirm incomplete catalog/glyph checks fail, then implement the missing definitions. Use frame-specific masks and insets; keep ornaments proportional while rails stretch/tile. Artwork is a subdued treatment of the original Luffy asset and uses its own logical opacity group.
- [ ] Generate and inspect native visual contact sheets for all192 background/frame combinations, all10 typography profiles on light/dark backgrounds, all3 artwork treatments and all12 toolbars on their matched frame and default frame. Include selected/paused samples. Fix clipped ornament, poor text contrast and asset mismatches; exact colors/insets are builder-owned within the approved references. Do not treat successful image export as visual approval.
- [ ] Rerun catalog/render checks; expect no missing assets, stable measurements and all PASS. Update manifest with actual paths, source provenance and font licenses; remove placeholder/fallback-only coverage before calling this task complete.

## Task 5: Build the approved appearance inspector and shared preview

**Files:** Create `Views/SettingsWindow.StopwatchAppearance.cs`, `Controls/StopwatchAppearancePreview.cs`, `tools/DesktopRegressionChecks/StopwatchAppearanceUiChecks.cs`; modify `SettingsWindow.axaml[.cs]`, `Program.cs` and any existing settings-category routing location found in those files. Reuse `SettingsChangeKind.OverlayAppearance`; add no redundant event kind unless the current pipeline demonstrably requires it.

**Interfaces:**

- `StopwatchAppearancePreview : UserControl`: `Apply(AppSettings settings)` uses a copied Nika profile for two sample ThemedClockFace controls and Task3 toolbar styler. `SetPreviewState(bool running, bool darkBackdrop)` only changes local fixture state. Samples use `00:44:27.0` / `Illustration`, selected100% and unselected configured opacity. No timer/session reference is held.
- SettingsWindow private methods `LoadStopwatchAppearance()`, `CommitStopwatchAppearance(bool rebuildControls = false)`, `RefreshStopwatchAppearancePreview()`. Follow existing settings save/error notification and `SettingsChanged` patterns; construction/loading does not commit.
- Stable native automation names: `StopwatchBackgroundChoice`, `StopwatchFrameChoice`, `StopwatchTypographyChoice`, `StopwatchArtworkChoice`, `StopwatchHoverControlsChoice`, `StopwatchBackgroundOpacity`, `StopwatchUnselectedOpacity`, and `StopwatchOpaque` + enum group name. Category key `StopwatchAppearance`; tabs Style/Transparency.
- `internal static void StopwatchAppearanceUiChecks.Run()` creates isolated inspector fixtures, uses existing test-only persistence suppression/injection patterns, and checks the real settings file hash before/after. Wire it to `--stopwatch-appearance-only` after native initialization.

- [ ] Add failing native inspector assertions: five selectors expose exact counts12/16/10/3/12, choose independently, retain values after reopen; initial defaults match spec; selecting a frame does not reset controls/background; Artwork checkbox disabled only forClean and remembered when re-enabled; slider endpoints and70/40 defaults are correct; state summary mentions fixed marker + ACTIVE behavior.
- [ ] Add preview-isolation assertions: Running/Paused and Light/Dark change only preview; no controller timer command is raised and settings serialize identically before/after these toggles. Changing a real style or slider issues one appropriate settings event and persists through the existing flow. Inject a save failure and assert a visible status while session preview retains the new choice; construction causes zero saves.
- [ ] Add layout checks at840×600 and1060×740, plus100%/150%/200% native scaling: preview visible, inspector vertical scrolling, selector popup choices reachable, no horizontal overflow or clipped Done button. Include keyboard navigation and accessible control names.
- [ ] Run focused native route; confirm intended failures. Implement the new category adjacent to Todo, two tabs, closed thumbnail-assisted selectors, shared existing text-size control and six opacity checkboxes. Reuse existing left preview host/category switching; preserve other settings and explicit Show/hide overlay action.
- [ ] Make theme ownership clear without silently changing application theme: if effective overlay is not Nika, show a compact `Use Nika overlay` action which changes only OverlayTheme. The new preview can show pending Nika choices while other themes are active. Keep application theme outside this category. Hide/disable legacy font/border/opacity editing controls when inapplicable to Nika and point to Stopwatch appearance; do not erase legacy values or let generic CommitControls copy stale legacy sliders into the new profile. Shared text size/color continue through their existing storage.
- [ ] Implement immediate commits through the existing pipeline with initialization guards and a readable save status; Done only closes. Use normalized copied settings for preview, then update all live overlays after real edits. Avoid rebuilding controls during slider drags or losing focus after a selection.
- [ ] Rerun the focused route; expect exit0, no fixture writes to live settings and matching live/preview renders. Inspect both tabs at minimum window size and capture final inspector screenshots.

## Task 6: Verify the full feature and document its actual limits

**Files:** Extend the new native check files only for unresolved end-to-end gaps; add `design/one-piece-v2/stopwatch-overlay/implementation-review/verification.md` and necessary comparison PNGs. Update the exploration manifest's production implementation status only after code actually changes, retaining selection history and the approved default.

**Interfaces:** No new product interfaces. Reuse Task1–5 fixtures and checks; do not create tests that merely repeat implementation constants without exercising behavior.

- [ ] Run `dotnet build StopwatchOverlay.Desktop/StopwatchOverlay.Desktop.csproj -m:1`, `dotnet test StopwatchOverlay.Tests/StopwatchOverlay.Tests.csproj -m:1`, and `dotnet run --project tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj -- --stopwatch-appearance-only`. Expected: successful build, all relevant tests/checks PASS, exit0; investigate failures rather than silently weakening checks. If restoration is blocked, use installed dependencies/`--no-restore` only when valid and report the limitation.
- [ ] Run existing desktop regressions `--nika-only`, `--todo-appearance-only`, and the full DesktopRegressionChecks command without a filter once. Expected: existing controller/Todo/clock behavior passes with adjusted Nika routing expectations only. Existing tests referencing legacy WPF do not replace the new native checks.
- [ ] Perform native behavior QA in isolated sessions for the four timing modes, combined and separate clocks, all four active/run combinations, reset, click-through, hover crossing/dismissal, close/reset confirmations, right-click, drag, shortcuts and hide/show. Verify selected timer identity and underlying time are unchanged by appearance edits. Check topmost/no-activate/capture behavior on the current OS and explicitly state any unavailable platform checks.
- [ ] Review the completed visual matrix and default live screenshots at real scale against approved boards. Measure controlled alpha pixels separately from subjective visual comparison. Confirm cached assets are not reopened/recreated every timer tick with an allocation/resource probe around repeated UpdateTime calls; no arbitrary performance target is claimed.
- [ ] Record actual commands/results, asset/font provenance, screenshot paths, display scale/OS, saved-setting isolation evidence and any remaining limits in verification.md. Mark any unrun checks as unverified. Require zero missing retained choices and no unresolved core behavior regressions before claiming completion.
- [ ] Review the final diff for scope, then obtain the independent review required by the selected execution workflow. Fix material findings and rerun only affected checks; avoid repeating broad suites after no further changes. Present the working result and verification, without committing/publishing unless authorized.

## Self-review and handoff

Spec coverage: catalogs/defaults/migration→Task1; default geometry/state→Task2; native behavior/theme route/popup opacity→Task3; complete visual library/fonts→Task4; inspector/preview→Task5; combined validation/evidence→Task6. All five Review Focus entries have owning tests. Contracts use the same profile, state and opacity types across live and preview rendering.

Recommended execution: **Native** in this session, with a final independent review. These six tasks share renderer, theme and settings contracts; one implementer can keep those coordinated with less repeated context. Subagent-driven execution remains available if the user prefers an independent review gate after every task. Review this plan and choose the execution method before product implementation, per the writing-plans workflow.
