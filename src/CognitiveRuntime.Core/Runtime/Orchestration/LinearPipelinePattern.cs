namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Describes a runtime-configured, ordered sequence of modes as pipeline
/// stages. Execution remains owned by <see cref="PatternExecutor"/>.
/// </summary>
public sealed class LinearPipelinePattern : IOrchestrationPattern
{
    public string Name => "linear-pipeline";

    public PatternExecutionPlan CreatePlan(PatternPlanRequest request) =>
        PatternPlanBuilder.CreateLinearPipeline(Name, request);
}
