using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Expresses the existing main -&gt; critic -&gt; revision loop as an
/// <see cref="IOrchestrationPattern"/>: every phase, in manifest order, sees
/// all prior phase results.
/// </summary>
public sealed class CriticRevisionPattern : IOrchestrationPattern
{
    public string Name => "critic-revision";

    public IReadOnlyList<PatternStep> Plan(LoadedMode mode) =>
        mode.Phases.Select(phase => new PatternStep(phase)).ToArray();

    public IReadOnlyList<PhaseResult> SelectContext(
        PatternStep step,
        IReadOnlyList<PhaseResult> completedResults) =>
        completedResults;
}
