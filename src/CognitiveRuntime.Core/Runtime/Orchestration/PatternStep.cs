using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

public sealed record PatternStep(LoadedPhase Phase)
{
    public string Name => Phase.Name;

    public PhaseKind Kind => Phase.Kind;
}
