using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Runs a single main phase whose output is the authoritative result. The
/// trivial pattern used to validate <see cref="IOrchestrationPattern"/>
/// before porting the critic-revision loop onto it.
/// </summary>
public sealed class SinglePassPattern : IOrchestrationPattern
{
    public string Name => "single-pass";

    public IReadOnlyList<PatternStep> Plan(LoadedMode mode)
    {
        var mainPhase = mode.Phases.First(phase => phase.Kind == PhaseKind.Main);
        return [new PatternStep(mainPhase)];
    }

    public IReadOnlyList<PhaseResult> SelectContext(
        PatternStep step,
        IReadOnlyList<PhaseResult> completedResults) =>
        completedResults;
}
