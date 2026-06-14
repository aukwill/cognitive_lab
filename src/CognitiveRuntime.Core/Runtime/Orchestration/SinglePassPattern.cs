namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Declares one main node whose output is authoritative.
/// </summary>
public sealed class SinglePassPattern : IOrchestrationPattern
{
    public string Name => "single-pass";

    public PatternExecutionPlan CreatePlan(PatternPlanRequest request) =>
        PatternPlanBuilder.CreateSinglePass(Name, request);
}
