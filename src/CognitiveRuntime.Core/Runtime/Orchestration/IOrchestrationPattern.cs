namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Creates an immutable, data-only execution plan. The runtime drives
/// execution and owns mode loading, model invocation, tracing, artifacts, and
/// evals.
/// </summary>
public interface IOrchestrationPattern
{
    string Name { get; }

    PatternExecutionPlan CreatePlan(PatternPlanRequest request);
}
