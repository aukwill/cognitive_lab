namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Declares the bounded main -&gt; critic -&gt; revision plan. Context edges
/// are explicit and the revision is authoritative.
/// </summary>
public sealed class CriticRevisionPattern : IOrchestrationPattern
{
    public string Name => "critic-revision";

    public PatternExecutionPlan CreatePlan(PatternPlanRequest request) =>
        PatternPlanBuilder.CreateCriticRevision(Name, request);
}
