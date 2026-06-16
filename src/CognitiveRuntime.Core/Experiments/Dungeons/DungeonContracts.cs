using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Experiments.Dungeons;

public sealed record DungeonExperimentRequest(
    string Brief,
    string ModelProvider,
    string OutputRoot,
    string ExperimentsRoot,
    string? InputSource = null,
    string? FlareGameRoot = null);

public sealed record DungeonPlan(
    int SchemaVersion,
    string Title,
    int Width,
    int Height,
    IReadOnlyList<DungeonRoom> Rooms,
    IReadOnlyList<DungeonCorridor> Corridors,
    IReadOnlyList<DungeonDoor> Doors,
    IReadOnlyList<DungeonMarker> Markers);

public sealed record DungeonRoom(
    string Id,
    int X,
    int Y,
    int Width,
    int Height);

public sealed record DungeonCorridor(
    string Id,
    string FromRoomId,
    string ToRoomId,
    int Width);

// Enum string casing comes from DungeonJson.Options (camelCase). A type-level
// converter would override that policy with PascalCase and contradict the
// lowercase values the prompts instruct the model to emit.
public enum DungeonDoorKind
{
    Open,
    Guarded,
    Locked
}

public sealed record DungeonDoor(
    string Id,
    string RoomAId,
    string RoomBId,
    DungeonDoorKind Kind);

public enum DungeonMarkerKind
{
    Entrance,
    Objective,
    Exit
}

public sealed record DungeonMarker(
    DungeonMarkerKind Kind,
    string RoomId);

public sealed record DungeonVerificationCheck(
    string Id,
    bool Passed,
    string Details);

public sealed record DungeonScoreComponent(
    string Id,
    int Points,
    string Details);

public sealed record DungeonVerificationReport(
    IReadOnlyList<DungeonVerificationCheck> Checks,
    IReadOnlyList<DungeonScoreComponent> ScoreComponents)
{
    public bool Feasible => Checks.All(check => check.Passed);

    public int Score => Feasible
        ? ScoreComponents.Sum(component => component.Points)
        : 0;
}

public sealed record DungeonPlanEvaluation(
    DungeonPlan? Plan,
    DungeonVerificationReport Report);

public sealed record DungeonCandidateResult(
    int Index,
    string ProposedJson,
    DungeonPlanEvaluation Proposed,
    string RevisedJson,
    DungeonPlanEvaluation Revised,
    string Directory);

public sealed record DungeonExperimentResult(
    RunResult Run,
    int? WinnerIndex,
    string? TmxPath,
    string? FlareMapPath,
    string? FlareModPath);

public sealed record DungeonExperimentPrompts(
    string Propose,
    string Revise);

internal sealed record DungeonLayout(
    int Width,
    int Height,
    bool[,] Walkable,
    IReadOnlyDictionary<string, DungeonPoint> RoomCenters);

internal sealed record DungeonPoint(int X, int Y);

public sealed record CompiledDungeon(
    string Tmx,
    string FlareMap,
    string SpawnMap,
    string ModSettings,
    string Attribution,
    string AsciiPreview);
