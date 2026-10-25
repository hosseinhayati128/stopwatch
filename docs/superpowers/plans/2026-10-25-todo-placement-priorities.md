# Todo placement and numeric priorities

Approved design: Add Todo has a single button toggling between ↑ Top and ↓ Bottom on each click, default Top each time. Placement changes manual ordering only; editing does not reposition tasks. Priorities are colored numbers 1–10, 1 highest. Existing priorities retain values 1–4, default remains 3. Preserve metadata, links, due dates and hidden completed positions.

- [x] Add regression checks for priorities 10/11 and top/bottom placement preserving existing order.
- [x] Extend organization storage and SaveTodo with optional new-item placement; resolve aliases and seed all existing tasks before inserting.
- [x] Add creation-only placement selector to editor and numeric ten-level menus/badges.
- [x] Run hidden smoke checks, publish, install and restart. Do not push unless asked.
