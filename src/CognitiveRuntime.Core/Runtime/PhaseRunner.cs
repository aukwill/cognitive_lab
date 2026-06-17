using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Runtime;

public sealed class PhaseRunner
{
    private readonly TimeProvider _timeProvider;

    public PhaseRunner(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PhaseResult> RunAsync(
        string runId,
        string executionNodeId,
        LoadedMode mode,
        LoadedPhase phase,
        string input,
        IReadOnlyList<PhaseResult> priorPhaseResults,
        IModelClient modelClient,
        ITraceSession trace,
        int attempt = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionNodeId);
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attempt),
                attempt,
                "Model call attempt must be at least 1.");
        }

        var phaseStartedAt = _timeProvider.GetUtcNow();
        var callId = CreateModelCallId(runId, executionNodeId, attempt);
        await trace.EmitAsync(
            TraceEventNames.PhaseStarted,
            new Dictionary<string, object?>
            {
                [TracePayloadKeys.NodeId] = executionNodeId,
                [TracePayloadKeys.Mode] = mode.Manifest.Name,
                [TracePayloadKeys.Phase] = phase.Name,
                [TracePayloadKeys.Kind] = phase.Kind.ToString().ToLowerInvariant()
            },
            cancellationToken);

        if (phase.Kind == PhaseKind.Critic)
        {
            await trace.EmitAsync(
                TraceEventNames.CriticStarted,
                new Dictionary<string, object?> { [TracePayloadKeys.Phase] = phase.Name },
                cancellationToken);
        }
        else if (phase.Kind == PhaseKind.Revision)
        {
            await trace.EmitAsync(
                TraceEventNames.RevisionStarted,
                new Dictionary<string, object?>
                {
                    [TracePayloadKeys.Phase] = phase.Name,
                    [TracePayloadKeys.PriorPhaseCount] = priorPhaseResults.Count
                },
                cancellationToken);
        }

        var modelStartedAt = _timeProvider.GetUtcNow();
        await trace.EmitAsync(
            TraceEventNames.ModelCalled,
            new Dictionary<string, object?>
            {
                [TracePayloadKeys.CallId] = callId,
                [TracePayloadKeys.NodeId] = executionNodeId,
                [TracePayloadKeys.Attempt] = attempt,
                [TracePayloadKeys.Provider] = modelClient.ProviderName,
                [TracePayloadKeys.Phase] = phase.Name
            },
            cancellationToken);

        ModelResponse response;
        try
        {
            response = await modelClient.CompleteAsync(
                new ModelRequest(
                    runId,
                    mode.Manifest.Name,
                    phase.Name,
                    phase.Kind,
                    phase.Prompt,
                    input,
                    Array.AsReadOnly(priorPhaseResults.ToArray())),
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                throw new ModelProviderException(
                    $"Provider '{modelClient.ProviderName}' returned empty content for phase '{phase.Name}'.");
            }
        }
        catch (Exception exception)
        {
            var failure = RuntimeFailureFactory.Create(
                exception,
                exception is OperationCanceledException
                    ? RuntimeFailureCategory.Cancellation
                    : RuntimeFailureCategory.Provider,
                phase.Name,
                modelClient.ProviderName);
            await EmitModelFailedBestEffortAsync(
                trace,
                callId,
                executionNodeId,
                attempt,
                modelStartedAt,
                failure,
                exception);
            throw;
        }

        var modelCompletedAt = _timeProvider.GetUtcNow();
        await trace.EmitAsync(
            TraceEventNames.ModelCompleted,
            new Dictionary<string, object?>
            {
                [TracePayloadKeys.CallId] = callId,
                [TracePayloadKeys.NodeId] = executionNodeId,
                [TracePayloadKeys.Attempt] = attempt,
                [TracePayloadKeys.Provider] = response.Provider,
                [TracePayloadKeys.Model] = response.Model,
                [TracePayloadKeys.Phase] = phase.Name,
                [TracePayloadKeys.ContentLength] = response.Content.Length,
                [TracePayloadKeys.DurationMs] = RuntimeDuration.GetMilliseconds(
                    modelStartedAt,
                    modelCompletedAt)
            },
            cancellationToken);

        if (phase.Kind == PhaseKind.Critic)
        {
            await trace.EmitAsync(
                TraceEventNames.CriticCompleted,
                new Dictionary<string, object?>
                {
                    [TracePayloadKeys.Phase] = phase.Name,
                    [TracePayloadKeys.ContentLength] = response.Content.Length
                },
                cancellationToken);
        }
        else if (phase.Kind == PhaseKind.Revision)
        {
            await trace.EmitAsync(
                TraceEventNames.RevisionCompleted,
                new Dictionary<string, object?>
                {
                    [TracePayloadKeys.Phase] = phase.Name,
                    [TracePayloadKeys.ContentLength] = response.Content.Length
                },
                cancellationToken);
        }

        var phaseCompletedAt = _timeProvider.GetUtcNow();
        await trace.EmitAsync(
            TraceEventNames.PhaseCompleted,
            new Dictionary<string, object?>
            {
                [TracePayloadKeys.NodeId] = executionNodeId,
                [TracePayloadKeys.Mode] = mode.Manifest.Name,
                [TracePayloadKeys.Phase] = phase.Name,
                [TracePayloadKeys.Kind] = phase.Kind.ToString().ToLowerInvariant(),
                [TracePayloadKeys.ContentLength] = response.Content.Length,
                [TracePayloadKeys.DurationMs] = RuntimeDuration.GetMilliseconds(
                    phaseStartedAt,
                    phaseCompletedAt)
            },
            cancellationToken);

        return new PhaseResult(
            phase.Name,
            phase.Kind,
            response.Content,
            response.Provider,
            response.Model);
    }

    private static string CreateModelCallId(
        string runId,
        string executionNodeId,
        int attempt) =>
        $"{runId}:{executionNodeId}:{attempt}";

    private async Task EmitModelFailedBestEffortAsync(
        ITraceSession trace,
        string callId,
        string executionNodeId,
        int attempt,
        DateTimeOffset startedAt,
        RuntimeFailureInfo failure,
        Exception exception)
    {
        try
        {
            var data = RuntimeFailureFactory.ToTraceData(failure);
            data[TracePayloadKeys.CallId] = callId;
            data[TracePayloadKeys.NodeId] = executionNodeId;
            data[TracePayloadKeys.Attempt] = attempt;
            data[TracePayloadKeys.Cancelled] = exception is OperationCanceledException;
            data[TracePayloadKeys.DurationMs] = RuntimeDuration.GetMilliseconds(
                startedAt,
                _timeProvider.GetUtcNow());
            await trace.EmitAsync(
                TraceEventNames.ModelFailed,
                data,
                CancellationToken.None);
        }
        catch
        {
            // The original model failure remains authoritative.
        }
    }
}
