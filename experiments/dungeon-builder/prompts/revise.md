Revise the proposed DungeonPlan using the deterministic verifier report.

Return exactly one complete JSON object. Do not use Markdown fences or
commentary. Preserve schema version 1 and obey all runtime-owned bounds.

Use exactly the same field names, types, and nesting as the proposed plan:
top-level `schemaVersion`, `title`, `width`, `height`, `rooms` (each with
`id`, `x`, `y`, `width`, `height`), `corridors` (each with `id`,
`fromRoomId`, `toRoomId`, `width`), `doors` (each with `id`, `roomAId`,
`roomBId`, `kind`), and `markers` (each with `kind` and `roomId`). Do not
rename, flatten, nest, or add fields, and do not introduce rectangles,
raw coordinates, asset names, tile IDs, file paths, event commands, or
scripts.

Fix every failed hard check first. Then improve the explicit score where
possible by adding an optional branch, a graph loop, objective separation,
and bounded room-size variety. Do not invent fields, file paths, tile IDs,
scripts, or additional markers.
