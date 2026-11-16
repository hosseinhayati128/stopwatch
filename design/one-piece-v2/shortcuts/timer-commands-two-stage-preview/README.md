# Timer Commands — quick hint, then expanded list

User direction: make the initial Win+F2 popup smaller; a second F2 press should show the previous three-column version.

## Proposed interaction

1. Win+F2 enters timer command mode and shows the user's selected quick actions, in reading order: Space — Start/stop, R — Reset timer, E — Edit timer, O — Show/hide overlay, T — Next timer, K — Choose a clock. The action set stays fixed while displayed keys follow the configured mappings.
2. A second deliberate F2 press while timer command mode is active expands the same popup to all assigned timer commands. The default interaction should support releasing Win before pressing F2 again or repeating Win+F2. Holding F2 is not intended to count as a second press.
3. Commands omitted from the quick hint should remain immediately usable. Expansion is for reference, not a prerequisite for dispatch. Escape retains its cancel meaning. Further F2 presses leave the expanded view open rather than adding a new collapse action.

The native prototype demonstrates local F2 expansion only, without global keyboard hooks or real command dispatch. The live application's current timer-command timeout is two seconds; timeout/chaining behavior, configured leaders, F2 mapping conflicts and repeat-key filtering must be addressed when implementing the interaction. Do not silently discard a user's existing F2 assignment.

## Visuals

Quick view: 620 x 128 logical units, six hints in two rows, small leader badge, synthetic Programming context, visible F2 / All commands hint. Expanded view: the previously reviewed 800 x 280 three-column layout. Both use unchanged 13-unit command labels and the same parchment/navy/gold styling. The expanded view keeps the top edge; positioning within real work areas still needs integration verification.

| Scale | Quick view | Expanded view |
|---|---|---|
| 70% | quick-70.png (434 x 90 pixels) | expanded-70.png (560 x 196 pixels) |
| 90% | quick-90.png (558 x 116 pixels) | expanded-90.png (720 x 252 pixels) |
| 125% | quick-125.png (775 x 160 pixels) | expanded-125.png (1000 x 350 pixels) |

custom-quick-90.png and custom-expanded-90.png use synthetic custom bindings, with unassigned Reset omitted. No live settings are loaded or saved.

## Verification

Fixture-only build succeeded with zero warnings/errors. All three scales and the custom sample passed native quick-action counts, expanded-command counts, footer overlap/row bounds, local F2 transition and top-edge preservation checks. All windows ran through Run-HiddenDesktop.ps1 on a private desktop. Images were visually inspected. Logs: .superpowers/sdd/2026-11-16-timer-commands-two-stage-preview.

Only the preview fixture is changed. Production UI, running app, timers, live shortcuts, installation and Git publication are untouched. No full unit suite was needed for this visual prototype; system-level shortcut behavior is not yet implemented or verified.
