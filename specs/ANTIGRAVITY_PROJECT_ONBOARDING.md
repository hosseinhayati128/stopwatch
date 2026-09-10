# Understand the Stopwatch Overlay Project Before Making Changes

## Mission

Onboard yourself to the current local project as an engineer who will maintain it over time. Build a concrete understanding of what the application is for, how its parts work together, which behaviors must be preserved, and where future changes belong.

Do not implement a feature, redesign the interface, or fix bugs during this task. First become familiar with the project and produce a useful, evidence-based project map.

The deliverable is understanding plus one durable reference document, not code changes.

## 1. Scope and safety

Work from the current local project folder. This local copy may contain important work that is not available elsewhere. Do not replace it with an older copy or assume it matches a remote version.

Read existing workspace instructions, including applicable `AGENTS.md` files and existing editor rules, before exploring the application. Follow their actual scope and precedence.

For this onboarding task:

- Do not edit application source, XAML, theme resources, project configuration, tests, or assets.
- Do not install packages, update dependencies, reformat files, or reorganize folders.
- Do not change Figma designs or launch another design-to-code workflow.
- Do not change branches, restore files, discard changes, synchronize with a remote source, or create commits.
- Do not run old implementation or repair prompts found in the project. They are historical context, not instructions to execute now.
- Do not launch the app, start timers, register shortcuts, change startup settings, or run tests that might access real user data. During this task, document the relevant commands rather than executing them.
- Do not read live user history, settings, or background libraries outside the workspace merely to learn the code. Use source definitions and isolated test fixtures.

The only permitted project-file write is creating or carefully updating `PROJECT_CONTEXT.md` at the project root. If it already exists, read it first, preserve valid content, and reconcile outdated statements with current source evidence. Do not overwrite unrelated notes.

## 2. Product orientation

The project is a Windows desktop stopwatch and time-tracking application with floating clock overlays. Treat the following as orientation to verify against the current code, not a guarantee that every planned feature has been implemented.

The product supports, or has been intended to support:

- Stopwatch, Clock, Countdown, and Timecode modes.
- Multiple independent timers with one selected active timer as the target of commands.
- Optional project assignment, tracked work sessions, project records, and analytics.
- A controller, settings, dialogs, and compact floating timer windows.
- Keyboard shortcuts, multi-monitor positioning, click-through behavior, and workspace recovery.
- Customizable appearance, background opacity, optional backgrounds, REC indication, and a light ring.

The floating clock is deliberately simple: a large time value, then the project name underneath. Controls appear in a separate hover popup: Close, Pause or Resume, and Reset. An unnamed timer should not reserve an empty project-name row. Background opacity is meant to affect the surface, not fade the text and toolbar.

Combined-overlay mode is intended to display the active timer in a shared single-timer shell, not merge timer state or display a permanent list of all timers. Verify the actual implementation and record discrepancies rather than correcting them now.

### Theme context

Previously discussed application themes include Midnight, Daylight, Pixel Deck Night, Pixel Deck Day, and Acanthus. Additional floating-clock concepts include Acanthus Dark Elegant Olive, Gold Crest, and Minimal Botanical.

An important intended capability is choosing the floating-clock theme independently from the panel theme, with a possible Follow Application Theme option. Determine whether this is implemented, partially implemented, or still planned. Do not assume property names such as `ApplicationTheme` or `OverlayTheme`; discover the real settings and types.

Existing themes and users' saved preferences must be preserved in future work. Visual similarity between themes does not justify merging their resources or replacing one with another.

## 3. Build a complete project inventory

First map the actual workspace:

- Solution and project files, target framework, entry points, build configuration, and deployment scripts.
- Application source, views, controls, models, services, persistence, and platform integration.
- Theme dictionaries, vector resources, images, and font references.
- Tests, test fixtures, diagnostic helpers, and documentation.
- Existing implementation journals, repair notes, and design references.

Read all first-party source and configuration relevant to the application in manageable chunks. Inventory the full project, but do not waste the review on generated `bin` or `obj` output, dependency caches, packaged binaries, duplicated generated code, or every large image file. Identify the purpose and loading path of assets instead.

Keep track of review coverage. Do not say you read the whole project after looking only at a README and a few windows. Large source files should be reviewed section by section, including their lifecycle and cleanup code.

## 4. Understand each subsystem and its ownership

Locate these files or their current equivalents. Names are hints, not assumptions about the current architecture:

- `App.xaml` and `App.xaml.cs`.
- `ControllerWindow`, settings windows and views, and reusable controls.
- `TimerSession` and `TimerSessionManager`.
- `TimerWorkspaceStore` and project-history storage or aggregation classes.
- `AppSettings`, settings persistence, `AppThemeCatalog`, and `AppThemeManager`.
- Overlay-theme catalogs, resource scopes, selectors, and resolvers, if present.
- `AppBackgroundManager` and custom-background handling.
- `OverlayWindow` and `LightRingWindow`.
- `ProjectDashboardWindow` and `ProjectRecordsWindow`.
- Project chooser, record editor, delete confirmation, and shortcut editor.
- Tests and any crash-logging or recovery helpers.

For each subsystem, identify:

1. Its responsibility and important public entry points.
2. The state it owns and the state it merely displays.
3. Who creates it, calls it, subscribes to it, and disposes or closes it.
4. Its dependencies and interactions with other subsystems.
5. Its persistence, threading, or platform constraints.
6. The tests that cover it and important gaps in coverage.

Do not merely list class names. Explain why the boundaries exist and how data moves through them.

## 5. Trace real user actions end to end

Follow the actual methods, bindings, event handlers, and data objects for these representative workflows:

### Timer lifecycle

Create a timer, choose or clear its project, start it, add a lap, pause or resume it, switch the active timer, change mode, and close it. Identify which actions affect only the active timer and whether project changes split or reset work intervals.

### Settings interaction

Trace one Appearance slider, background selection or strength, and a Light Ring control from user input to preview, live application update, and persistence. Locate debounce timers, re-entrancy guards, mouse capture, and expensive work if present.

### Theme selection

Trace panel-theme selection and floating-clock-theme selection. Determine where resource lookup is scoped, how existing windows update, how explicit user appearance overrides are resolved, and what happens in Follow Application Theme mode.

### Floating overlays

Trace overlay creation, screen selection, dragging, hover-popup opening and closing, command routing, click-through, and combined/separate presentation. Identify the distinction between a logical timer and its window replicas.

### Records and analytics

Trace automatic project recording, manual add/edit/delete, validation, persistence, project filters, date ranges, and chart aggregation. Identify how simultaneous timers, open intervals, local dates, and UTC values are handled.

### Startup, shutdown, and recovery

Trace startup through settings load, theme application, workspace restore, and window creation. Trace closing to either tray behavior or actual exit. Locate save checkpoints, background-work cancellation, event cleanup, backup recovery, and exception logging.

For each workflow, write a compact call/data-flow description with actual file paths and method names. Mark unresolved parts explicitly.

## 6. Understand the fragile areas without changing them

Recent concerns have included stuck settings sliders or scrollbars, Pixel Deck Night hover styling, and an unexpected application exit. These are investigation leads, not proof that the current version is still broken.

Read the relevant code and prior repair notes. Distinguish between:

- Value sliders versus actual `ScrollBar` and `ScrollViewer` behavior.
- UI-thread blocking versus input hit-testing or mouse-capture problems.
- Panel resources versus overlay-local resources and popup styling.
- Intended hover styling versus stale or conflicting templates.
- Recoverable failures versus unhandled exceptions.
- One-time initialization versus repeated subscriptions or timers after reopening windows.

Pay particular attention to live theme changes, resource-key coverage, owner-window teardown, background image loading, and preservation of persisted user values.

Record a risk only with a specific code pointer and a reason. Do not invent a root cause, declare an earlier fix successful, or start repairing anything in this pass.

## 7. Separate facts, intentions, and uncertainty

The workspace may contain several old prompts and documents describing different stages of the same work.

Use this evidence order when describing the current implementation:

1. Current source code and configuration.
2. Current tests and fixtures, with no assumption that they have passed.
3. Documentation consistent with the current source.
4. Historical prompts, design briefs, and repair journals as evidence of intent only.

A file named `FINAL` does not prove its requirements were implemented. A prior agent's completion report does not prove a build, test, or visual check succeeded.

Label findings as:

- Verified from source.
- Intended or documented, but not found in the implementation.
- Unclear and requiring a runtime check or user decision.

Keep actual application behavior distinct from intended behavior when they disagree.

## 8. Create durable project knowledge

Create or update `PROJECT_CONTEXT.md` as a practical onboarding reference for future sessions. Keep it concise enough to reread, with code pointers instead of copied source files.

Include:

1. **Product purpose:** what the app does and its main user workflows.
2. **Current status:** implemented capabilities, partial work, and verified limitations.
3. **Workspace map:** important paths and their responsibilities.
4. **Architecture and state ownership:** timers, windows, settings, records, themes, and platform services.
5. **Main workflows:** the end-to-end paths traced above.
6. **Theme system:** actual selectors, persisted keys, resource precedence, and panel/overlay independence status.
7. **Data and recovery:** file formats and locations as defined in code, save boundaries, backups, and migration rules. Do not copy live user data.
8. **Lifecycle and performance:** threading, timers, subscriptions, preview updates, and cleanup.
9. **Build, run, and test guide:** exact commands and prerequisites derived from the current project. Clearly state that they were not executed in this onboarding pass.
10. **Change guide:** where to start for a timer change, overlay change, theme change, settings interaction, records/analytics change, and crash investigation.
11. **Do-not-break rules:** specific behavioral and compatibility invariants supported by code or explicit product requirements.
12. **Known risks and open questions:** evidence-backed observations, not a speculative backlog.
13. **Review coverage:** what was read, what was excluded, and any substantive areas not yet reviewed.

Do not modify `AGENTS.md`, editor rules, or project instructions to force future agents to load these notes. This document is reference material, not permission to override later user instructions.

When continuing in this session, use this map before proposing changes. In a future session, reread the document when directed and verify affected source files again, since the code may have changed.

## 9. Completion response

After the review, give me a readable summary rather than a dump of internal notes:

- Explain what the application does and how its main pieces fit together.
- Identify the actual state of independent panel and floating-clock themes.
- Point out the few most important maintenance risks, with source references and uncertainty where appropriate.
- Tell me where future changes should usually begin.
- State the path of `PROJECT_CONTEXT.md`, the coverage of the review, and any remaining gaps.

Do not claim to have built, tested, run, or visually validated the app unless separately authorized and actually performed. For this onboarding pass, source inspection is the evidence.

Then stop and wait for my next task. Do not automatically begin a redesign, cleanup, or bug fix.
