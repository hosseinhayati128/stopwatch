# Nika's Chronicle - Todo overlay design

Status: implemented and verified in Desktop, updated 2026-10-30. Source, approved designs and native captures are maintained together on `codex/nika-chronicle`. See [verification.md](verification.md) for checks and native captures.

The approved boards are the visual source of truth. Keep their layout, typography, palette, borders, proportions and artwork placement when implementing or changing this theme. Do not replace them with a new interpretation during implementation.

The Desktop app now has a **Todo appearance** Settings category, with independent **Classic** and **Pixel Voyage** profiles and all retained alternatives. Production artwork is embedded in `StopwatchOverlay.Desktop/Assets/Todo/`; provenance is in `runtime-artwork.md`. Actual native window captures are in `implemented/`. Original boards and prompts are preserved unchanged. The design-stage notes below record the decisions that led to implementation; future-tense statements in those historical notes are not the current delivery status.

User corrections, 2026-10-30: Luffy reuses the original One Piece skull from `Assets/Pirate/icons.png` throughout the header, transparent launcher and picker. Each profile has **Launcher: same as header**; unlinking restores its saved independent launcher choice. Pixel subtask hold progress uses the same square segmented meter as parent tasks.

The subsequent fidelity pass restores the fine double gold trim, stronger native task lettering, centered action labels, aligned task summaries, quieter map detail under text and consistent editor controls. [Reference / before / after comparison](polish-comparison.html). Previous native captures are preserved in `implemented/before-polish-2026-10-30/`; approved boards remain unchanged.

## Retained decisions

- Keep all four background options from `background-options-approved.png`: Navigator Parchment, Amber Vellum, Midnight Chart and Quiet Atlas.
- Keep the navy header, subtle map/coastline engraving, ivory title and thin brass divider.
- Keep the first Luffy skull from `header-options-reference.png`. The other three icons from that board were rejected; the idea of illustrated header backgrounds was liked.
- Preserve Zoro, Nami, Brook and Chopper from `crew-emblems-first-four-approved.png` as approved style references.
- User requested all crew emblems as Settings alternatives. The new `crew-emblems-16-preview.png` expands the collection to ten Straw Hats plus Law, Whitebeard, Ace, Buggy, Shanks and Roger. The new artwork is a custom interpretation of official emblem references, not official released artwork or a verified exact reproduction.
- Background, header emblem and header decoration should be independent future Settings choices. Retaining Luffy as the initial default is an implementation assumption based on the first approved header, not a new explicit default selection.

## Preview collection

Numbers 01-10: Luffy, Zoro, Nami, Usopp, Sanji, Chopper, Robin, Franky, Brook, Jinbe.

Numbers 11-16: Law, Whitebeard, Ace, Buggy, Shanks, Roger.

The board is one comparison image, not sixteen production icon files. At implementation time, create separate transparent icons, check silhouettes at the actual header size and preserve circular proportions. Review the newly generated details against the official references before treating them as final. Do not crop the entire board into runtime controls or hardcode a fixed-size picture over the UI.

## Reference provenance

- [Official ten-member flag collection](https://one-piece.com/news/78909/index.html): source for the Straw Hat emblem family. Downloaded artwork `references/official-straw-hat-collection-00.jpg`; companion stamp book preview `references/official-straw-hat-collection-01.jpg`.
- [Tokyo One Piece Tower announcement](https://prtimes.jp/main/html/rd/p/000000067.000013812.html): official attraction reference for crew emblems and Ace. Viewed in the browser; its image download did not complete, so there is no local copy.
- [Official COSPA pirate flags](https://one-piece.com/news/o20141027_1501/index.html): Shanks, Whitebeard and Law. Local files `official-red-hair-flag.jpg`, `official-whitebeard-flag.jpg` and `official-heart-flag.jpg` under `references/`.
- [Official Buggy flag jacket announcement](https://one-piece.com/news/o20160829_4431/index.html): `references/official-buggy-flag.jpg`.
- [Official Oro Jackson model announcement](https://one-piece.com/news/o20221210_15299/index.html): Roger flag on the sails, `references/official-roger-ship.jpg`.

The reference downloads are research inputs, not app-ready assets. The generated board used the approved four-icon board, official ten-member sheet, Heart Pirates, Whitebeard and Red Hair flag images as its five direct image inputs. Ace, Buggy and Roger were also guided by the inspected official artwork described in the prompt. Generation used the built-in image tool; the exact final prompt is saved in `emblem-board-prompt.txt`.

## Next design layer

Task rows and checkboxes: the user retained options **1 Ship's Ledger**, **2 Parchment Cards**, **5 Cartographer's Notes**, and **8 Pixel Voyage** as future Settings alternatives. Ship's Ledger is the default from the accepted combined preview. Source boards are `task-rows-checkboxes-options-01-04.png` and `task-rows-checkboxes-options-05-08.png`. Keep row style independent of background and emblem.

Priority markers: the user retained the **original default circles**, **3 Ledger Stamps**, and **4 Pixel Ranks**. Preserve the original flat colored circles from the row previews as the default; the later Enamel Pins exploration is not a replacement. The two alternatives are in `priority-markers-options-01-04.png`. Keep priority-marker style independent of row style. The other priority designs remain exploration history only.

Subtasks and notes: after reviewing the full panel, the user narrowed the choices to **1 Margin Notes** and **2 Foldout Card**, switchable in Settings. Margin Notes remains the default shown in the accepted combined preview. Keep the detail style independent of the parent-row style and priority marker. Pixel Journal and Branch Ledger remain exploration history only. This does not remove the separately approved Pixel Voyage rows, Pixel Ranks priorities or Pixel Controls toolbar. Preserve inline expansion, completion counts, existing hold-to-complete behavior, note/link actions and detail context menus.

Toolbar and Add todo button: the user retained **1 Classic Brass** and **4 Pixel Controls** from `toolbar-actions-options-01-04.png`. Classic Brass is the default from the accepted combined preview. Keep this appearance choice independent of row, priority-marker and detail styles. Navigator Bar and Ink & Paper remain exploration history only. Preserve the Priority/Manual sort selector, Show completed toggle and existing Add todo flow.

Preserve the existing numeric priority levels 1-10, with 1 highest, and the click-to-change menu. All retained alternatives remain planned Settings choices. No production implementation has been made during these design selections.

## Combined full-panel preview

`combined-classic-preview-v1.png` combines Navigator Parchment, the Luffy emblem, subtle engraved navy header, Ship's Ledger rows, original priority circles, Margin Notes details and Classic Brass controls. It shows the first task expanded with one of three subtasks completed and a note, alongside three other parent tasks.

The user accepted this classic combination, refining subtasks and notes to Margin Notes and Foldout Card only. Keep the shown Margin Notes treatment as the default. Navigator Parchment, Luffy, Ship's Ledger, original circles and Classic Brass form the rest of the default combination. All other retained alternatives stay in the independent Settings plan. Generated with the built-in image tool; the exact prompt is in `combined-classic-preview-v1-prompt.txt`. Implementation is the next stage; these files currently record the approved design only.

## Additional Pixel Voyage preview

At the user's request, `combined-pixel-preview-v1.png` combines the previously retained **8 Pixel Voyage** parent rows and checkboxes, **4 Pixel Ranks** priority badges, and **4 Pixel Controls** toolbar and footer. It retains Navigator Parchment and the engraved navy Luffy header, matching the illustrated artwork of the earlier pixel boards.

For expanded subtasks and notes, this preview uses **Foldout Card**, one of the two retained detail styles. Margin Notes remains selectable and remains the classic default. Pixel Journal has not been restored as an option. The sample shows priority levels 1, 3, 6 and 10, three open parent tasks, one completed parent, and the first task expanded with one of three subtasks completed and a note.

The user accepted this additional pixel combination. It does not replace the accepted classic preview or any Settings defaults. Generated with the built-in image tool using the five existing design references; the exact prompt is saved in `combined-pixel-preview-v1-prompt.txt`. No production code or Settings controls have been changed.

## Collapsed launcher exploration

`collapsed-launcher-options-01-04.png` compares four treatments using the same Luffy emblem: **1 Crew Crest** (free-standing emblem), **2 Navy Seal** (navy circular medallion), **3 Map Tile** (warm parchment square), and **4 Pixel Crest** (pixel emblem and stepped navy tile). This board is now exploration history: the user chose the full earlier crew-emblem collection as standalone transparent logos instead of the framed treatments.

Each tile includes an enlarged concept and a smaller illustrative view. These smaller views are not verified runtime renders; in particular, the small Pixel Crest thumbnail simplifies the sprite styling. Before implementation, create separate transparent assets and check each selected treatment at the current 72-logical-pixel button size, including the pixel version at appropriate display scales. Preserve click-to-expand/collapse, dragging, keyboard focus and accessible naming. No count badge or new behavior is proposed in this step.

Generated with the built-in image tool using the accepted pixel panel and the earlier header reference; the exact prompt is in `collapsed-launcher-options-01-04-prompt.txt`. This board is design exploration, not production launcher assets.

## Selected collapsed launcher

Offer all **16 crew emblems** from the earlier collection in Settings: Luffy, Zoro, Nami, Usopp, Sanji, Chopper, Robin, Franky, Brook, Jinbe, Law, Whitebeard, Ace, Buggy, Shanks and Roger. Display only the selected logo on a genuinely transparent background, matching the current free-standing launcher. No medallion, tile, map background, decorative frame or task-count badge. Keep Luffy as the current default and allow the launcher emblem to be selected independently of the header emblem.

Use `crew-emblems-16-preview.png` as the design reference, not as a ready-to-use asset sheet. Create individual transparent emblems during implementation, check their silhouettes at 72 logical pixels and preserve the current launcher interactions. This selection is recorded for implementation; no app controls or runtime assets have been changed. Next design layer: task states and interaction feedback.

## Task states and completion feedback exploration

`task-states-feedback-options-01-04.png` compares **1 Quiet Ledger**, **2 Soft Labels**, **3 Margin Signals**, and **4 Pixel Signals**. Each panel uses the same Ship's Ledger rows and original priority circles, with an in-progress task, future scheduled deadline, due-soon task, overdue task and completed task. The user selected **1 Quiet Ledger for the normal Classic Todo theme** and **4 Pixel Signals for Pixel Voyage**, including the matching hold-to-complete feedback. Quiet Ledger uses inline state/date cues with a thin hold-progress line; Pixel Signals uses stepped labels with a segmented hold meter. Soft Labels and Margin Signals remain exploration history only. The approved row and priority choices remain unchanged.

The four HOLD FEEDBACK strips beneath the panels are magnified explanatory snapshots of a partly completed hold, not permanent footer controls. They compare a thin progress line, a tracing checkbox outline, rising brass checkbox fill, and a segmented meter. The checkbox remains unchecked until the existing one-second threshold is reached. Preserve mouse/Space input, cancellation behavior, the independent double-click in-progress toggle, and existing deadline semantics (soon within 24 hours; overdue at or before now). Completed tasks should have no active urgency cue, while their stored deadline remains available. Icons and text supplement color.

The shortened dates illustrate a 29 October afternoon scenario. Runtime display must retain full deadline precision, including year and time, through visible text or accessible details. No behavior changes are approved by this comparison. Generated with the built-in image tool from the accepted Classic and Pixel Voyage panels; the exact prompt is in `task-states-feedback-options-01-04-prompt.txt`. These selections are recorded for implementation; no runtime controls have changed. Next step: supporting views and menus.

## Task menus and Add/Edit dialogs

The user approved both coordinated sheets for their corresponding normal Classic and Pixel Voyage themes. Classic is on the left and Pixel Voyage on the right. The numbers identify surfaces within each sheet, not four competing theme choices.

- `task-menus-classic-pixel-v1.png`: task context menus at the top, priority selectors at the bottom. Retain Edit task, Add > Note/Subtask, conditional Link/Relink, and Delete. The Link example assumes Telegram is enabled; linked tasks use Relink. Priority selectors retain all ten levels with 1 highest. The mockups show priority 3 selected; production selection follows the actual task. Preserve the current priority colors rather than treating generated color differences as new requirements.
- `task-editors-classic-pixel-v1.png`: Add Todo at the top, Edit Todo below. Retain the single multiline body, priority selector, optional local due date/time with Clear, and checklist editing with completion, up/down, removal and Add subtask. Manual position belongs only to Add. The top examples show the deadline disabled; the lower examples show it enabled. Both modes still support optional deadlines. Preserve Save/Save changes, Ctrl+Enter, Escape, focus, scrolling, resizing, validation and save/sync error handling.

Approved styling uses restrained parchment surfaces and brass borders for Classic, with stepped controls and frames for Pixel Voyage. These previews do not change the separately selected overlay subtask/note layouts. Standalone note/subtask editing remains a simpler text-only dialog; those dialogs, deletion confirmations, and empty/error states have not been newly rendered in these sheets. Existing deletion confirmation text and its local/sync scope must remain intact.

Both sheets were generated with the built-in image tool using the accepted Classic and Pixel panels. Exact prompts are saved in `task-menus-classic-pixel-v1-prompt.txt` and `task-editors-classic-pixel-v1-prompt.txt`. No production code, runtime assets or Settings controls changed. Next step: supporting empty/error states and small dialogs, followed by the appearance Settings layout and implementation with checks at actual overlay size.

## Supporting states and small dialogs

The user approved all three matched Classic/Pixel sheets. Classic is on the left and Pixel Voyage on the right; numbering identifies examples rather than competing theme choices.

- `empty-completed-classic-pixel-v1.png`: empty list and all-done states, both with zero open tasks. Show completed is off in the all-done sample, and the message explains how to reveal finished tasks. Keep Add todo available. Missing notes-folder configuration and read failures must retain their separate messages.
- `save-sync-feedback-classic-pixel-v1.png`: close-ups of editor message areas and footers. A failed local save keeps editable input and Save changes. A saved-local sync warning keeps input read-only and offers Close. The approved presentation removes redundant Cancel and replaces the save hint with Esc to Close only after the local save succeeded; it reuses the existing Close behavior. Two small inline Telegram retry notices appear beneath the examples. These notices represent the existing retry state, not a guarantee that every sync issue resolves automatically. Actual diagnostic messages must remain available and wrap without clipping.
- `small-dialogs-classic-pixel-v1.png`: deletion confirmation for a task with a linked Telegram message, a compact Add note editor, and an Edit subtask editor. The delete message explicitly names the local task file and linked Telegram message, and warns that the action cannot be undone. Unlinked tasks and detail deletions must use their own existing scope text. Text-only note/subtask dialogs retain Save, Cancel and their keyboard shortcuts, without parent priority, deadline or completion controls. The same form applies to Add subtask and Edit note.

These sheets retain the approved map parchment and navy headers, with gently rounded Classic controls and stepped Pixel controls. The feedback panels are cropped examples, not substitutes for the full approved editor forms. No production implementation has been made.

Generated with the built-in image tool. Exact prompts are saved in `empty-completed-classic-pixel-v1-prompt.txt`, `save-sync-feedback-classic-pixel-v1-prompt.txt`, and `small-dialogs-classic-pixel-v1-prompt.txt`. All three boards are approved for their respective themes. The appearance Settings layout is the next design layer.

## Appearance Settings and emblem picker

`appearance-settings-preview-v1.png` proposes a focused **Todo appearance** category within the existing Settings & Overlay Inspector structure: sample preview on the left, navigation in the middle, and scrollable controls on the right. The page controls Todo appearance, independently of other application and floating-clock settings. It shows Classic selected; Pixel Voyage uses the same host page with its corresponding preview and selections.

The two theme styles start with the previously approved combinations. The proposal remembers later customizations separately for each style rather than discarding them when switching. Four background thumbnails expose Navigator Parchment, Amber Vellum, Midnight Chart and Quiet Atlas. Header and launcher emblems have separate selectors. Four dropdowns preserve all retained task-row styles (1/2/5/8), priority markers (original circles/3/4), subtask/note layouts (Margin Notes/Foldout Card), and toolbar styles (Classic Brass/Pixel Controls). The checked Engraved header control proposes toggling only the map decoration. Task feedback follows the approved theme mapping: Quiet Ledger for Classic and Pixel Signals for Pixel Voyage.

`appearance-emblem-picker-v1.png` shows the shared sixteen-emblem picker for the header target. The launcher selector reuses this picker with its own title and selection. A pick is staged until Use emblem; Cancel or close discards it. Selection-cell boundaries belong to the picker UI, never to the standalone transparent launcher. The artwork is still a reference collection, not a set of finalized production assets.

Choices apply immediately in the proposed Settings interaction, with Done closing the window, consistent with the current inspector. The preview uses sample data, reflects appearance changes, and must never edit or synchronize real tasks. Persistence failures must be reported without a silent selection reset. Keep artwork proportions, keyboard access, readable labels and scrolling at smaller window sizes. Existing settings categories remain available.

Both previews are awaiting user review. Generated with the built-in image tool from the two accepted overlays, four-background board and sixteen-emblem board. Exact prompts: `appearance-settings-preview-v1-prompt.txt` and `appearance-emblem-picker-v1-prompt.txt`. No runtime code or saved user settings have changed. After this layout is accepted, proceed to implementation planning, asset preparation, and checks at actual overlay size.
