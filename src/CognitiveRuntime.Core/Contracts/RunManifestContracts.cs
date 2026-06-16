namespace CognitiveRuntime.Core.Contracts;

public static class RunManifestSchema
{
    public const int CurrentVersion = 1;
}

public sealed record RunManifestMode(
    string SourceId,
    string Name,
    int? Version,
    string? Lens);

public sealed record RunManifestPlanNode(
    string Id,
    string Kind,
    string ModeSourceId,
    string PhaseKind,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> ContextInputs,
    string? InputNodeId,
    string? StageId);

public sealed record RunManifestPlanStage(
    string Id,
    int Index,
    string ModeSourceId,
    IReadOnlyList<string> NodeIds,
    string AuthoritativeNodeId,
    string? InputNodeId);

public sealed record RunManifestPlan(
    string PatternName,
    IReadOnlyList<RunManifestPlanNode> Nodes,
    IReadOnlyList<RunManifestPlanStage> Stages,
    string AuthoritativeNodeId,
    IReadOnlyList<string> RequiredEvalNodeIds,
    bool EvaluateLoopEfficacy);

public sealed record RunManifestExecutionNode(
    string NodeId,
    string? StageId,
    string Phase,
    string PhaseKind,
    string Mode,
    string Status,
    string? Provider,
    string? Model,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int? OutputLength);

public sealed record RunManifestStageOutcome(
    string StageId,
    int Index,
    string Mode,
    string Status,
    IReadOnlyList<string> NodeIds);

public sealed record RunManifestArtifact(
    string RelativePath,
    string Kind,
    string MediaType,
    long? ByteLength,
    string? Sha256);

public sealed record RunManifestEvalSummary(
    bool Passed,
    int CheckCount,
    int PassedCount,
    int FailedCount);

public sealed record RunManifest(
    int SchemaVersion,
    string RunId,
    string RequestedMode,
    string PatternName,
    string ModelProvider,
    IReadOnlyList<string> Models,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string LifecycleStatus,
    RunOutcome Outcome,
    IReadOnlyList<RunManifestMode> Modes,
    RunManifestPlan Plan,
    IReadOnlyList<RunManifestExecutionNode> ExecutionNodes,
    IReadOnlyList<RunManifestStageOutcome> Stages,
    IReadOnlyList<RunManifestArtifact> Artifacts,
    RunManifestEvalSummary? Evaluation,
    RuntimeFailureInfo? Failure);
