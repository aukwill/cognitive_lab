# AR-012: Verifier-Guided Flare Dungeon Builder

Status: Research complete; promote a bounded experiment

Date: 2026-06-14

## Decision

Build the first verifier-guided generation experiment around an isometric
dungeon that can be opened in Tiled and played in Flare.

Use three representations with distinct ownership:

1. The model proposes `DungeonPlan` JSON.
2. Runtime code compiles the plan to a Tiled TMX map.
3. Runtime code exports the same plan to a Flare map file.

TMX is the visual authoring and inspection format. Flare's INI-style map is
the executable game format. `DungeonPlan` is the small, typed contract that
the verifier can reason about without trusting model-authored XML, tile IDs,
asset paths, or game events.

Do not ask the model to emit TMX or Flare map text directly.

## Hypothesis

A fixed generate, verify, revise, and select loop can produce a dungeon that:

- satisfies deterministic topology and playability constraints
- opens as a valid map in the open-source Tiled editor
- loads as a playable map in the open-source Flare engine
- improves its verifier score after one bounded revision

The experiment fails if map validity depends on subjective review, an LLM
judge, manual repair, or model control over the verifier or loop.

## Why Tiled And Flare

Tiled is an open-source map editor with a documented TMX format. Flare is an
open-source action RPG engine whose official game repository keeps source maps
as TMX and exports runtime maps in a documented INI-style format.

The official Flare dungeon template uses:

- isometric orientation
- `192` by `96` logical tiles
- `background`, `object`, and `collision` tile layers
- object groups such as `event` and `enemy`
- the dungeon tileset definition
  `tilesetdefs/tileset_dungeon.txt`

Flare's map documentation defines the runtime header, tile layers, events,
enemies, enemy groups, and NPC sections. This gives the experiment a real
consumer rather than a bespoke preview.

## Smallest Useful Experiment

Timebox: three implementation days after the required runtime pattern exists.

Generate three candidates for one fixed brief:

> Build a compact crypt with an entrance, three to six rooms, one optional
> branch, one locked or guarded objective room, and a reachable exit.

For each candidate:

1. Parse `DungeonPlan` JSON.
2. Reject invalid structure.
3. Compile walkable cells, walls, doors, entrance, objective, and exit.
4. Run deterministic verification.
5. Return structured verifier failures to the model once.
6. Verify the revised candidate.
7. Select the highest-scoring feasible candidate using stable plan order as
   the tie-breaker.
8. Compile the winner to TMX and Flare map artifacts.

The initial experiment does not generate arbitrary scripts, quests, loot
tables, enemy definitions, or asset paths.

## DungeonPlan Version 1

The model-facing contract should describe dungeon semantics, not engine tile
IDs:

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

Version 1 constraints:

- Dimensions are fixed or bounded by runtime configuration.
- Room rectangles must be in bounds and may not overlap.
- Room and corridor IDs are unique.
- Corridors connect declared rooms.
- Door kinds come from a runtime-owned enum.
- Exactly one entrance, objective, and exit are required.
- Asset names, tile IDs, file paths, event commands, and scripts are absent.
- The runtime chooses all concrete tiles and Flare event definitions.

The compiler may use a fixed seed to choose decorative variants after the
semantic layout passes verification.

## Deterministic Verification

Separate hard feasibility from optional score.

Hard checks:

- JSON matches the supported schema version.
- Counts and dimensions remain within configured limits.
- Rooms are in bounds, non-overlapping, and large enough to navigate.
- Every corridor connects declared rooms.
- The room graph is connected from the entrance.
- The objective and exit are reachable.
- Walls and collision tiles agree.
- Door cells do not block the only required route unless their runtime-owned
  condition is satisfiable.
- TMX layers have exactly `width * height` cells.
- TMX references only allowlisted bundled tilesheets.
- Flare sections and layer dimensions match the header.
- All artifact paths remain inside the run directory.

Optional score:

- one point for an optional branch
- one point for a loop in the room graph
- one point for a distinct objective room
- one point for path-length separation between entrance and objective
- one point for bounded room-size variety
- penalties for excessive dead space, corridor crossings, or repeated shapes

The score must never make an infeasible candidate selectable.

## Baseline And Negative Control

Baseline:

- A deterministic hand-authored valid `DungeonPlan` fixture.
- It must compile to TMX and Flare map output and pass every hard check.

Negative controls:

- disconnected objective room
- overlapping rooms
- corridor referencing a missing room
- duplicate entrance
- exit outside the map
- collision layer blocking the required route
- unknown tile or asset reference introduced after compilation
- malformed TMX cell count
- malformed Flare layer row width

Each mutation must fail under a stable verifier check ID.

## Runtime Pattern

The runtime owns this fixed plan:

```text
candidate-1 -- verify -- revise -- verify
candidate-2 -- verify -- revise -- verify
candidate-3 -- verify -- revise -- verify
                                      |
                         deterministic select
                                      |
                         compile TMX and Flare
```

The first spike may execute candidates sequentially. Parallel scatter-gather
is an optimization, not a prerequisite for proving verifier-guided revision.

The model cannot:

- add candidates or revisions
- alter constraints or scores
- select the winner
- provide tile IDs or artifact paths
- declare verification successful
- write TMX or Flare files

## Artifacts

```text
candidates/
  01/
    proposed.json
    verification.json
    revised.json
    revised_verification.json
  02/
  03/
winner/
  dungeon_plan.json
  dungeon.tmx
  dungeon.txt
  verification.json
decision_ledger.md
trace.json
eval_report.md
```

Optional local tooling may render `dungeon.tmx` to `dungeon.png`, but PNG
rendering is not a correctness requirement.

## Trace Requirements

Trace candidate proposal, verifier start and completion, revision, selection,
compilation, and artifact writes. Verifier payloads should record stable check
IDs and bounded diagnostics rather than copying complete map files.

Selection records:

- candidate feasibility
- score components
- selected candidate ID
- tie-break rule
- reason each losing candidate was not committed

## Playable Integration

The generated Flare map should be packaged as a small local mod requiring the
official `fantasycore` assets. A one-cell `spawn.txt` can use an `on_load`
event to transfer the player into the generated dungeon, matching the pattern
used by Flare's official `devlab` mod.

The experiment should copy or reference Flare assets only when the human has
provided a local Flare game-data root. Core tests use synthetic tile metadata
and do not require Flare, Tiled, downloads, or external credentials.

Flare game art and data are CC-BY-SA 3.0 or later. Generated bundles that
include or adapt those assets must preserve attribution and share-alike
requirements. The runtime's own semantic plan and compiler remain separate
from those assets.

## Reproduction Target

Credential-free verification:

```powershell
dotnet test
dotnet run --project src/CognitiveRuntime.Cli -- `
  --experiment dungeon-builder `
  --run-mode mock
```

Human visual verification:

1. Open `winner/dungeon.tmx` in Tiled.
2. Inspect background, object, and collision layers.
3. Confirm entrance, objective, and exit placement.
4. Install the generated local mod beside a compatible Flare engine and
   official game-data checkout.
5. Start a new game with the generated mod enabled and walk from entrance to
   objective and exit.

## Limitations

- Tiled can prove format readability, not game design quality.
- Flare loading proves compatibility, not that an encounter is fun.
- Topology scores are explicit proxies and should not be described as
  aesthetic quality.
- The first compiler targets one Flare dungeon tileset and one TMX subset.
- External Tiled and Flare smoke tests remain opt-in; normal tests stay
  credential-free and self-contained.

## Disposition

Promote as a bounded `AR-012` runtime experiment.

Implement the semantic plan, verifier, and deterministic compiler before
adding model calls. Prove the complete loop with `MockModelClient`, then use an
external provider only as an optional demonstration.

Do not wait for general-purpose scatter-gather. Do not generalize this into a
game engine, procedural-generation framework, or arbitrary verifier shell.

## Sources

- [Tiled TMX map format](https://doc.mapeditor.org/en/stable/reference/tmx-map-format/)
- [Tiled command-line interface](https://doc.mapeditor.org/en/stable/manual/command-line/)
- [Flare Engine map-file documentation](https://github.com/flareteam/flare-engine/wiki/Map-Files)
- [Flare Game repository](https://github.com/flareteam/flare-game)
- [Official Flare dungeon template](https://github.com/flareteam/flare-game/blob/master/tiled/dungeon/dungeon_template.tmx)
- [Official Flare devlab mod](https://github.com/flareteam/flare-game/tree/master/mods/devlab)
