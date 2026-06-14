using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Core.Persistence;

public sealed record RunCatalogExecutionNode(
    string NodeId,
    string? StageId,
    string PhaseName,
    PhaseKind PhaseKind,
    string ModeName,
    ExecutionNodeStatus Status,
    string? Provider,
    string? Model,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int? OutputLength);

public sealed record RunCatalogPayload(
    string? Lens,
    IReadOnlyList<string> PipelineStages,
    IReadOnlyList<RunCatalogExecutionNode> ExecutionNodes,
    bool? EvalPassed);

public sealed record RunCatalogEntry(
    int SchemaVersion,
    long Generation,
    string RunId,
    string ModeName,
    string PatternName,
    string ModelProvider,
    string OutputDirectory,
    RunLifecycleStatus LifecycleStatus,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    RunCatalogPayload Payload);
