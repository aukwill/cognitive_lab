You are proposing one dungeon plan inside a runtime-owned experiment.

Return exactly one JSON object matching the DungeonPlan schema below.
Do not use Markdown fences. Do not add commentary. Use exactly these field
names, types, and nesting; do not rename, flatten, nest, or add fields.

```json
{
  "schemaVersion": 1,
  "title": "The Ashen Reliquary",
  "width": 24,
  "height": 24,
  "rooms": [
    { "id": "entrance", "x": 2, "y": 10, "width": 5, "height": 5 },
    { "id": "sanctum", "x": 16, "y": 8, "width": 6, "height": 7 }
  ],
  "corridors": [
    {
      "id": "main-passage",
      "fromRoomId": "entrance",
      "toRoomId": "sanctum",
      "width": 2
    }
  ],
  "doors": [
    {
      "id": "sanctum-door",
      "roomAId": "entrance",
      "roomBId": "sanctum",
      "kind": "guarded"
    }
  ],
  "markers": [
    { "kind": "entrance", "roomId": "entrance" },
    { "kind": "objective", "roomId": "sanctum" },
    { "kind": "exit", "roomId": "entrance" }
  ]
}
```

Field notes:

- `width`/`height` (map) must each be between 12 and 48.
- `rooms` must contain 3 to 6 entries. Each room's `x`/`y`/`width`/`height`
  must keep the room at least 3x3, inside the map, and at least one cell
  away from every edge. Rooms may not overlap.
- `corridors` is a list (use `[]` if none). Each `width` is 1 or 2.
  `fromRoomId`/`toRoomId` must reference declared room `id`s and differ.
- `doors` is a list (use `[]` if none). `kind` is one of `"open"`,
  `"guarded"`, or `"locked"`. `roomAId`/`roomBId` must reference declared
  room `id`s and differ.
- `markers` must contain exactly one entry each for `kind` `"entrance"`,
  `"objective"`, and `"exit"`, each with a `roomId` referencing a declared
  room.
- Do not include asset names, tile IDs, file paths, event commands, scripts,
  rectangles, coordinates other than the fields above, or any other fields.

The runtime owns verification, revision count, selection, tile IDs, file
paths, rendering, and completion. You may only propose rooms, corridors,
doors, and the three required markers.

Create a compact crypt with three to six non-overlapping rooms, an entrance,
an objective, an exit, and corridors that make every room reachable.
