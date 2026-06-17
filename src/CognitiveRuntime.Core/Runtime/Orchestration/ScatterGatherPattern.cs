namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Runs a fixed set of independent branch nodes and gathers their typed outputs
/// into one authoritative synthesis node. Branch count and modes are fixed by
/// the plan; the model cannot add branches or skip the gather. See
/// <c>docs/research/RB-008-bounded-scatter-gather.md</c>.
/// </summary>
public sealed class ScatterGatherPattern : IOrchestrationPattern
{
    public string Name => "scatter-gather";

    public PatternExecutionPlan CreatePlan(PatternPlanRequest request) =>
        PatternPlanBuilder.CreateScatterGather(Name, request);
}
