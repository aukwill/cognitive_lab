using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// The outcome of a single grouped stage executed by
/// <see cref="PatternExecutor"/>.
/// </summary>
public sealed record PipelineStageResult(
    int StageIndex,
    string ModeName,
    LoadedMode Mode,
    IReadOnlyList<PhaseResult> PhaseResults,
    string ResultContent,
    string RevisionContent,
    string StageDirectory);
