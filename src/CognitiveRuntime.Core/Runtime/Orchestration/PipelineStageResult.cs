using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// The outcome of running a single stage of a <see cref="LinearPipelinePattern"/>:
/// a complete main-&gt;critic-&gt;revision run of one mode.
/// </summary>
public sealed record PipelineStageResult(
    int StageIndex,
    string ModeName,
    LoadedMode Mode,
    IReadOnlyList<PhaseResult> PhaseResults,
    string ResultContent,
    string RevisionContent,
    string StageDirectory);
