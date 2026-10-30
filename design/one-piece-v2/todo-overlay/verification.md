# Todo appearance verification — updated 2026-10-30

The approved boards remain the visual source of truth. Original images and prompts were preserved. Implementation targets the Avalonia Desktop project built by `publish.ps1`.

## Delivered

- Nika-only Classic and Pixel Voyage appearance profiles, with independent persisted choices.
- Four backgrounds, sixteen independently selectable header/transparent launcher emblems, four row styles, three priority styles, two detail layouts and two toolbar styles.
- Todo appearance inspector with sample-only preview, staged emblem selection and visible persistence failures.
- The sample preview renders the shared native components to an in-memory snapshot at a fixed scale, then fits that image uniformly inside Settings. This avoids nested scaled-control clipping; opening the category refreshes it after native templates attach. It never opens task storage or synchronizes data.
- Native Todo editors, details, confirmations, task feedback and empty/completed states. Existing task storage and synchronization handlers remain in place.

## Checks

Build: `dotnet build tools/DesktopRegressionChecks/DesktopRegressionChecks.csproj --no-restore -m:1 -p:CopyLocalLockFileAssemblies=false -p:UseSharedDesktopRuntime=true` — zero warnings/errors. Shared runtime avoids duplicating large desktop resources in test output.

The native regression runner uses the Desktop runtime configuration and dependency manifest, with `C:/Users/h128/.nuget/packages` as an additional probing path. Checked suites:

- `--todo-appearance-only`: settings migration and JSON normalization, independent profiles and emblems, retained settings choices, native window geometry, staged picker cancellation, scoped editor styling, input preservation, empty/completed states, hold completion/cancellation and transparent emblem isolation.
- `--nika-only`: controller appearance/resources, non-Nika restoration and mode selection through refresh.
- `--todo-add-only`: existing One Piece/Daylight/Midnight Todo labels, centered Add button across font sizes and the Add event.

Tests use workspace-local temporary task files. The legacy Add test required process-local TEMP/TMP redirection into the workspace because its first run could not create the temporary task fixtures. No production tasks, settings, sync accounts or startup registration were changed. The live-data migration test was excluded. The production app was not restarted, and `publish.ps1` was not executed.

## Review and visual evidence

One fresh read-only review identified minimum-width inspector clipping, equal-cell emblem contamination and missing stepped Pixel input frames. These were reproduced and repaired with focused checks. The preview note font was corrected as part of the user's explicit requirement to retain reference typography. No review findings were deferred.

Final inspection also corrected preview clipping and initial missing button text. Checks now verify preview containment and actual footer pixels, not only that a screenshot file was produced.

`implemented/` contains 23 native window captures and sixteen individual transparent emblem renders. Compare `classic-overlay.png` and `pixel-overlay.png` with the approved combined boards. Supporting captures include Settings at 1060×700 and 840×600, linked emblems, Pixel subtask hold progress, the emblem picker, both editors, save-warning footers, detail/delete dialogs and empty/all-done screens. Artwork scales uniformly; native text and inputs remain interactive.

## User corrections — 2026-10-30

- Restored Luffy using the existing original One Piece skull renderer and asset. Todo and original renders match pixel-for-pixel at 38, 64, 72 and 160 pixels; header, launcher, previews and picker share this path.
- Added **Launcher: same as header** per profile. JSON round trips retain the link preference and the separate launcher selection. Native checks change the header while linked, switch profiles and unlink; the launcher follows the header while linked and restores its previous independent choice afterward. The settings UI disables the independent launcher picker while linked and emits its normal persistence notification.
- Replaced the Pixel subtask's continuous progress bar with a square segmented meter bound to the existing hold progress. Native checks exercise partial progress and cancellation, and verify Classic retains its prior bar. Existing completion behavior also passes.

The new regression checks first reproduced all three reported problems. After the fixes, the full regression-project build passed with zero warnings/errors; `--todo-appearance-only`, `--nika-only` and `--todo-add-only` all passed. Inspected `pixel-subtask-hold.png`, `settings-linked-emblems.png` and the restored `emblems/luffy.png` directly. This remains a local source update; no publish script, production restart or Git publication was performed.

## Reference fidelity refinement — 2026-10-30

Compared both combined reference boards and the editor board directly with native captures. Restored the double gold outline, corrected rounded-shell clipping, strengthened task titles with the embedded Inter ExtraBold face, and softened map contrast under the reading area. Task summaries now align with titles; checkbox, badge and title rows use symmetric spacing. Action lettering receives a whole-pixel optical correction, and checklist controls share consistent sizing. Pixel row outlines and editor placement controls now follow their retained stepped treatment.

The native Add button initially placed visible lettering three pixels below center. The final pixel check measures its lettering center at `(176,20.5)` inside a `354×42` button, within one pixel horizontally and half a pixel vertically. Task badge and text layout centers both measure 22. The reference's four-task Pixel composition fits its viewport without clipping the final row. Editor checks cover the Pixel placement button inside scroll content, which previously missed styling because the visual tree had not attached.

The final build completed with zero warnings/errors. Fresh `--todo-appearance-only` and `--todo-add-only` runs pass, covering native layouts, centered text, profile and emblem settings, task interaction, transparency and restoration of other themes. The controller suite was already verified before this Todo-only refinement. Original reference images were not edited. [Reference / before / after comparison](polish-comparison.html) uses preserved previous captures and the final native renders.

## Working decisions

- Reused the existing `codex/nika-chronicle` checkout so it remains the source used by the user's publish script. Implementation and verification were completed locally before the user requested Git publication.
- Used PowerShell checks and a local execution ledger on Windows instead of the skill's Bash wrappers. Verification commands and results are recorded here; scratch fixtures/logs are ignored under `.superpowers/`.
- The exact-fidelity instruction includes the Settings preview's note typography, so that small review finding was corrected instead of deferred.

## Optional sidebar artwork layer — 2026-10-30

Settings → Todo appearance → Sidebar artwork layer offers all 17 existing sidebar motifs plus None. Each profile independently retains its selection and opacity; the initial opacity is 18%, and existing settings keep None. The selected map stays beneath the layer. Header and launcher emblems, controller choices and the collapsed transparent launcher are independent.

The renderer removes the navy backing, applies sepia on parchment or gold on Midnight Chart, feathers crop edges and fits the source uniformly. Cached prepared images use one draw so fractional opacity stays linear. The initial nested-mask implementation failed the half-opacity test at 0.121; the corrected renderer measures 0.500.

The full regression-project build has zero warnings/errors. Fresh `--todo-mural-only`, `--todo-appearance-only`, and `--todo-reorder-only` runs pass. Tests cover all 17 transparent motifs, old/invalid settings and persistence, profile/controller independence, live selection and opacity, retaining the active slider, native preview updates, compact Settings bounds, theme switching, collapsed launcher isolation, and existing task completion/drag behavior. Test fixtures confirm real settings are unchanged. Native captures `mural-classic.png`, `mural-pixel.png`, `mural-midnight.png`, `mural-settings.png` and `mural-full-opacity.png` are in `implemented/`.

A fresh read-only code review found no actionable defects. Verification used actual Avalonia windows and automated native checks; no physical OS mouse session or production app restart was performed. Source artwork and prior approved reference images remain unchanged. The update is local and ready for the normal publish workflow.

### Artwork centering correction

The user reported that the layer was not centered. Rendered-pixel checks reproduced a 16–44 pixel downward displacement at 384×480, with additional horizontal offsets from uneven atlas padding. Removed the deliberate lower placement and now center each motif using its visible alpha bounds, measured once and cached independently of tint. The complete source crop and uniform scale remain intact, preserving faint details without stretching.

The new centering regression covers all 17 motifs in portrait and wide backgrounds. It allows 1.25% of the smaller dimension for resampling of isolated edge texture pixels; the corrected implementation passes, along with opacity linearity, transparency, profiles, preview updates and theme restoration. Both builds completed with zero warnings/errors; `--todo-mural-only` and `git diff --check` pass. Refreshed native Classic and Midnight captures were visually inspected. This follow-up only changes artwork placement and remains unpublished.

### Artwork dropdown scrolling crash

The user's crash logs showed a `NullReferenceException` in the artwork item template during `VirtualizingStackPanel.RecycleElement`. Avalonia clears the content of a recycled popup row, invoking the template with a null item. The template now returns no visual for that temporary empty state instead of dereferencing it.

Added a native popup regression that opens the actual dropdown, scrolls repeatedly between its first and last entries, checks recycled thumbnails and labels, selects the last artwork, then reopens it in both Classic and Pixel/Midnight configurations. The old code reproduced the same null-reference failure. The corrected code passes this regression, all mural/centering checks and the existing appearance suite. Final build: zero warnings/errors; `git diff --check` passes. No production settings or task data were changed, and the published app was not restarted.

### Main emblem alignment within decorative margins

The user's Luffy screenshot exposed a limitation of centering the complete decoration: asymmetric faint clouds can be centered while the skull itself remains shifted right. Native raster measurements reproduced horizontal offsets of 7.5–10.5 pixels at 384×480 for Straw Hat Crest, Heart Pirates and Ace's Flame, and 6–6.5 pixels at 480×240. Whitebeard was already correctly placed and explicitly approved by the user.

Those three motifs now use the stronger engraving for their horizontal anchor, retaining the existing full-decoration vertical anchor. Uniform scaling accounts for both source edges around the adjusted anchor so faint surrounding details remain inside the panel. Other motifs retain their previous placement and scale; source images and shared controller atlas crops are unchanged.

The new rendered-pixel regression failed before the fix and passes afterward for all 17 motifs at portrait and wide sizes. The full mural suite also passes dropdown recycling, transparency, linear opacity, settings/profile independence and native preview checks. Build: zero warnings/errors. Whitebeard's isolated before/after PNGs have identical SHA-256 hashes. Inspected [the updated native Luffy capture](implemented/mural-luffy-centered.png); `git diff --check` passes. No production restart, settings changes or publication was performed.

### Complete 17-artwork alignment audit

The user reported remaining offsets and clarified that Sun Warrior was too far right, not corrupted. The previous alpha-threshold checks were insufficient: they verified the decorative silhouette rather than the visually identifying subject. Compared all 17 source motifs, isolated native renders, and expanded-panel captures. A universal ink-weighted center was also rejected because it displaced already-balanced Whitebeard and flame emblems.

Replaced automatic threshold centering and the three special cases with [17 documented subject anchors](artwork-alignment.md). Sun Warrior now centers its figure/shield composition instead of allowing the sword to determine its placement. Other asymmetric motifs have individual references; balanced crest axes are retained. Uniform fitting reserves room on both sides of each anchor, maintaining the complete source crop and safe margins in narrow or wide panels. Original bitmap files and controller crops are unchanged.

The revised native regression first reproduced subject offsets of approximately 24 px for Sun Warrior, 17 px for Zunesha and 48 px for Laboon at 384×480, plus several smaller offsets. It now checks independently annotated landmarks recovered from rendered pixels for every motif, at 384×480, 480×240 and 576×720, using both tints and 30% opacity. All pass, including source containment, actual expanded-window layer centering, dropdown recycling, transparency and settings/profile behavior. The broader Todo appearance suite also passes. Full build: zero warnings/errors; `git diff --check` passes.

Visually inspected [all 17 native panels](implemented/artwork-audit/all-17-panels.png), [all 17 isolated motifs with center guides](implemented/artwork-audit/all-17-guides.png), and the full-size Sun Warrior panel. Individual panel captures are saved alongside both galleries. Guides exist only in the audit renderer. This is a local source fix; the publish script and production app were not run or restarted.
