using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Declares the ordered steps a run executes and how each step's context is
/// assembled from prior results. The runtime drives execution and owns model
/// invocation, tracing, artifacts, and evals; a pattern only describes the
/// plan and context-selection rules.
/// </summary>
public interface IOrchestrationPattern
{
    string Name { get; }

    IReadOnlyList<PatternStep> Plan(LoadedMode mode);

    IReadOnlyList<PhaseResult> SelectContext(
        PatternStep step,
        IReadOnlyList<PhaseResult> completedResults);
}
