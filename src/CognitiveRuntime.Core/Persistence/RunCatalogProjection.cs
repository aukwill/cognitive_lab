using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime;

namespace CognitiveRuntime.Core.Persistence;

internal static class RunCatalogProjection
{
    public const int CurrentSchemaVersion = 1;

    public static RunCatalogEntry Create(
        RunRequest request,
        RunState state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);

        return new RunCatalogEntry(
            CurrentSchemaVersion,
            GetGeneration(state.LifecycleStatus),
            state.RunId,
            request.ModeName,
            state.Plan.PatternName,
            request.ModelProvider,
            state.Artifacts.RunDirectory,
            state.LifecycleStatus,
            state.Outcome,
            createdAt,
            updatedAt,
            new RunCatalogPayload(
                request.Lens,
                request.PipelineStages?.ToArray() ?? [],
                state.ExecutionNodes
                    .Select(node => new RunCatalogExecutionNode(
                        node.NodeId,
                        node.StageId,
                        node.PhaseName,
                        node.PhaseKind,
                        node.ModeName,
                        node.Status,
                        node.Provider,
                        node.Model,
                        node.StartedAt,
                        node.EndedAt,
                        node.OutputLength))
                    .ToArray(),
                state.EvalReport?.Passed));
    }

    private static long GetGeneration(RunLifecycleStatus status) =>
        status switch
        {
            RunLifecycleStatus.Created => 1,
            RunLifecycleStatus.Running => 2,
            RunLifecycleStatus.ExecutionCompleted => 3,
            RunLifecycleStatus.Evaluating => 4,
            RunLifecycleStatus.Finalizing => 5,
            RunLifecycleStatus.Succeeded or
            RunLifecycleStatus.Failed or
            RunLifecycleStatus.Cancelled => 6,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                null)
        };
}
