using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

public enum PatternNodeKind
{
    Phase
}

public sealed record PatternPlanRequest(
    string ModeName,
    string? Lens,
    IReadOnlyList<string>? PipelineStages,
    IReadOnlyList<string>? ScatterModes = null);

public sealed record PatternModeSource(
    string Id,
    string ModeName,
    string? Lens = null);

public sealed record PatternExecutionNode(
    string Id,
    PatternNodeKind Kind,
    string ModeSourceId,
    PhaseKind PhaseKind,
    IReadOnlyList<string> DependencyNodeIds,
    IReadOnlyList<string> ContextNodeIds,
    string? InputNodeId = null,
    string? StageId = null,
    // Optional relative directory (under the run directory) for this node's
    // phase output. Lets a pattern group phase artifacts -- e.g. scatter-gather
    // branches under scatter/NN-<mode>/ -- without the runtime branching on the
    // pattern name. Null means the run-root phases/ directory.
    string? ArtifactGroup = null);

public sealed record PatternStage(
    string Id,
    int Index,
    string ModeSourceId,
    IReadOnlyList<string> NodeIds,
    string AuthoritativeNodeId,
    string? InputNodeId = null);

public sealed record PatternEvalProfile(
    IReadOnlyList<string> RequiredNodeIds,
    bool EvaluateLoopEfficacy);

public sealed record PatternExecutionPlan(
    string PatternName,
    IReadOnlyList<PatternModeSource> ModeSources,
    IReadOnlyList<PatternExecutionNode> Nodes,
    IReadOnlyList<PatternStage> Stages,
    string AuthoritativeNodeId,
    PatternEvalProfile EvalProfile);
