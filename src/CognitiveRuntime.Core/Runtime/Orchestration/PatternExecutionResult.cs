using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

public sealed record PatternNodeExecutionResult(
    PatternExecutionNode Node,
    LoadedMode Mode,
    LoadedPhase Phase,
    string Input,
    IReadOnlyList<string> ContextNodeIds,
    PhaseResult PhaseResult);

public sealed record PatternExecutionResult(
    PatternExecutionPlan Plan,
    IReadOnlyDictionary<string, LoadedMode> LoadedModes,
    IReadOnlyList<PatternNodeExecutionResult> NodeResults,
    IReadOnlyList<ExecutionNodeState> NodeStates,
    IReadOnlyList<PipelineStageResult> Stages,
    LoadedMode AuthoritativeMode,
    IReadOnlyList<PhaseResult> EvalPhaseResults,
    string ResultContent);
