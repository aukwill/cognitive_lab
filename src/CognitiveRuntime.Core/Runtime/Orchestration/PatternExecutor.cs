using System.Collections.ObjectModel;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

public sealed class PatternExecutor
{
    private readonly IModeLoader _modeLoader;
    private readonly PhaseRunner _phaseRunner;
    private readonly IArtifactWriter _artifactWriter;
    private readonly TimeProvider _timeProvider;

    public PatternExecutor(
        IModeLoader modeLoader,
        PhaseRunner phaseRunner,
        IArtifactWriter artifactWriter,
        TimeProvider timeProvider)
    {
        _modeLoader = modeLoader;
        _phaseRunner = phaseRunner;
        _artifactWriter = artifactWriter;
        _timeProvider = timeProvider;
    }

    public async Task<PatternExecutionResult> ExecuteAsync(
        string runId,
        string initialInput,
        PatternExecutionPlan plan,
        IModelClient modelClient,
        RunArtifactPaths rootArtifacts,
        ITraceSession trace,
        Action<IReadOnlyList<ExecutionNodeState>>? nodeStatesChanged = null,
        CancellationToken cancellationToken = default)
    {
        var nodeStates = new ExecutionNodeStateTracker(plan, _timeProvider);
        PublishNodeStates(nodeStates, nodeStatesChanged);

        try
        {
            var loadedModes = await LoadModesAsync(plan, trace, cancellationToken);
            var phasesByNodeId = ResolvePhases(plan, loadedModes);
            var resultsByNodeId =
                new Dictionary<string, PatternNodeExecutionResult>(
                    StringComparer.Ordinal);
            var orderedResults =
                new List<PatternNodeExecutionResult>(plan.Nodes.Count);
            var stageResults =
                new List<PipelineStageResult>(plan.Stages.Count);

            if (plan.Stages.Count == 0)
            {
                foreach (var node in plan.Nodes)
                {
                    var result = await ExecuteNodeAsync(
                        runId,
                        initialInput,
                        node,
                        loadedModes,
                        phasesByNodeId,
                        resultsByNodeId,
                        modelClient,
                        trace,
                        nodeStates,
                        nodeStatesChanged,
                        cancellationToken);
                    resultsByNodeId.Add(node.Id, result);
                    orderedResults.Add(result);
                }
            }
            else
            {
                foreach (var stage in plan.Stages)
                {
                    var modeSource = plan.ModeSources.Single(
                        source => string.Equals(
                            source.Id,
                            stage.ModeSourceId,
                            StringComparison.Ordinal));
                    var stageInput = ResolveInput(
                        initialInput,
                        stage.InputNodeId,
                        resultsByNodeId);

                    await trace.EmitAsync(
                        TraceEventNames.StageStarted,
                        new Dictionary<string, object?>
                        {
                            [TracePayloadKeys.StageId] = stage.Id,
                            [TracePayloadKeys.StageIndex] = stage.Index,
                            [TracePayloadKeys.Mode] = modeSource.ModeName
                        },
                        cancellationToken);

                    var stageNodeResults = new List<PatternNodeExecutionResult>(
                        stage.NodeIds.Count);
                    foreach (var nodeId in stage.NodeIds)
                    {
                        var node = plan.Nodes.Single(
                            candidate => string.Equals(
                                candidate.Id,
                                nodeId,
                                StringComparison.Ordinal));
                        var result = await ExecuteNodeAsync(
                            runId,
                            initialInput,
                            node,
                            loadedModes,
                            phasesByNodeId,
                            resultsByNodeId,
                            modelClient,
                            trace,
                            nodeStates,
                            nodeStatesChanged,
                            cancellationToken);
                        resultsByNodeId.Add(node.Id, result);
                        orderedResults.Add(result);
                        stageNodeResults.Add(result);
                    }

                    await trace.EmitAsync(
                        TraceEventNames.StageCompleted,
                        new Dictionary<string, object?>
                        {
                            [TracePayloadKeys.StageId] = stage.Id,
                            [TracePayloadKeys.StageIndex] = stage.Index,
                            [TracePayloadKeys.Mode] = modeSource.ModeName,
                            [TracePayloadKeys.PhaseCount] = stageNodeResults.Count
                        },
                        cancellationToken);

                    var mode = loadedModes[stage.ModeSourceId];
                    var phaseResults = stageNodeResults
                        .Select(result => result.PhaseResult)
                        .ToArray();
                    var stageResultContent = ResultComposer.Compose(
                        mode,
                        phaseResults);
                    var authoritativeContent =
                        resultsByNodeId[stage.AuthoritativeNodeId]
                            .PhaseResult
                            .Content;
                    var stageArtifacts = await _artifactWriter.PrepareStageAsync(
                        rootArtifacts,
                        stage.Index,
                        modeSource.ModeName,
                        cancellationToken);

                    await _artifactWriter.WriteAsync(
                        stageArtifacts,
                        ArtifactKind.Input,
                        stageInput,
                        cancellationToken);
                    await _artifactWriter.WriteAsync(
                        stageArtifacts,
                        ArtifactKind.Result,
                        stageResultContent,
                        cancellationToken);

                    stageResults.Add(
                        new PipelineStageResult(
                            stage.Index,
                            modeSource.ModeName,
                            mode,
                            phaseResults,
                            stageResultContent,
                            authoritativeContent,
                            stageArtifacts.RunDirectory));
                }
            }

            var authoritativeResult = resultsByNodeId[plan.AuthoritativeNodeId];
            var evalNodeResults = plan.EvalProfile.RequiredNodeIds
                .Select(nodeId => resultsByNodeId[nodeId])
                .ToArray();
            var evalPhaseResults = evalNodeResults
                .Select(result => result.PhaseResult)
                .ToArray();
            var resultContent = ResultComposer.Compose(
                authoritativeResult.Mode,
                evalPhaseResults);

            return new PatternExecutionResult(
                plan,
                new ReadOnlyDictionary<string, LoadedMode>(
                    new Dictionary<string, LoadedMode>(
                        loadedModes,
                        StringComparer.Ordinal)),
                Array.AsReadOnly(orderedResults.ToArray()),
                nodeStates.Snapshot,
                Array.AsReadOnly(stageResults.ToArray()),
                authoritativeResult.Mode,
                evalPhaseResults,
                resultContent);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            var unfinished = nodeStates.Snapshot
                .Where(state => state.Status is
                    ExecutionNodeStatus.Pending or ExecutionNodeStatus.Running)
                .ToArray();
            if (nodeStates.CancelUnfinished())
            {
                PublishNodeStates(nodeStates, nodeStatesChanged);
                await EmitNodeCancelledBestEffortAsync(
                    trace,
                    GetUpdatedStates(nodeStates, unfinished));
            }

            throw;
        }
        catch
        {
            var unfinished = nodeStates.Snapshot
                .Where(state => state.Status is
                    ExecutionNodeStatus.Pending or ExecutionNodeStatus.Running)
                .ToArray();
            if (nodeStates.CancelUnfinished())
            {
                PublishNodeStates(nodeStates, nodeStatesChanged);
                await EmitNodeCancelledBestEffortAsync(
                    trace,
                    GetUpdatedStates(nodeStates, unfinished));
            }

            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, LoadedMode>> LoadModesAsync(
        PatternExecutionPlan plan,
        ITraceSession trace,
        CancellationToken cancellationToken)
    {
        var loadedModes = new Dictionary<string, LoadedMode>(StringComparer.Ordinal);
        foreach (var source in plan.ModeSources)
        {
            var mode = await _modeLoader.LoadAsync(
                source.ModeName,
                source.Lens,
                cancellationToken);
            loadedModes.Add(source.Id, mode);

            await trace.EmitAsync(
                TraceEventNames.ModeLoaded,
                new Dictionary<string, object?>
                {
                    [TracePayloadKeys.SourceId] = source.Id,
                    [TracePayloadKeys.Name] = mode.Manifest.Name,
                    [TracePayloadKeys.Version] = mode.Manifest.Version,
                    [TracePayloadKeys.PhaseCount] = mode.Phases.Count,
                    [TracePayloadKeys.Lens] = source.Lens
                },
                cancellationToken);
        }

        return loadedModes;
    }

    private async Task<PatternNodeExecutionResult> ExecuteNodeAsync(
        string runId,
        string initialInput,
        PatternExecutionNode node,
        IReadOnlyDictionary<string, LoadedMode> loadedModes,
        IReadOnlyDictionary<string, LoadedPhase> phasesByNodeId,
        IReadOnlyDictionary<string, PatternNodeExecutionResult> completedResults,
        IModelClient modelClient,
        ITraceSession trace,
        ExecutionNodeStateTracker nodeStates,
        Action<IReadOnlyList<ExecutionNodeState>>? nodeStatesChanged,
        CancellationToken cancellationToken)
    {
        var mode = loadedModes[node.ModeSourceId];
        var phase = phasesByNodeId[node.Id];
        nodeStates.Start(
            node.Id,
            phase.Name,
            mode.Manifest.Name,
            modelClient.ProviderName);
        PublishNodeStates(nodeStates, nodeStatesChanged);
        await trace.EmitAsync(
            TraceEventNames.NodeStarted,
            CreateNodeTraceData(
                node,
                mode.Manifest.Name,
                phase.Name,
                "running"),
            cancellationToken);

        try
        {
            if (node.DependencyNodeIds.Any(
                    dependencyId => !completedResults.ContainsKey(dependencyId)))
            {
                throw new InvalidOperationException(
                    $"Pattern node '{node.Id}' has a failed or incomplete dependency.");
            }

            var input = ResolveInput(
                initialInput,
                node.InputNodeId,
                completedResults);
            var context = node.ContextNodeIds
                .Select(contextNodeId => completedResults[contextNodeId].PhaseResult)
                .ToArray();

            var phaseResult = await _phaseRunner.RunAsync(
                runId,
                node.Id,
                mode,
                phase,
                input,
                context,
                modelClient,
                trace,
                attempt: 1,
                cancellationToken: cancellationToken);
            nodeStates.Complete(node.Id, phaseResult);
            PublishNodeStates(nodeStates, nodeStatesChanged);
            var completedState = nodeStates.Snapshot.Single(
                state => string.Equals(
                    state.NodeId,
                    node.Id,
                    StringComparison.Ordinal));
            var completedData = CreateNodeTraceData(
                node,
                mode.Manifest.Name,
                phase.Name,
                "completed");
            completedData[TracePayloadKeys.Provider] = phaseResult.Provider;
            completedData[TracePayloadKeys.Model] = phaseResult.Model;
            completedData[TracePayloadKeys.OutputLength] = phaseResult.Content.Length;
            AddDuration(completedData, completedState);
            await trace.EmitAsync(
                TraceEventNames.NodeCompleted,
                completedData,
                cancellationToken);

            return new PatternNodeExecutionResult(
                node,
                mode,
                phase,
                input,
                node.ContextNodeIds,
                phaseResult);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            var unfinished = nodeStates.Snapshot
                .Where(state => state.Status is
                    ExecutionNodeStatus.Pending or ExecutionNodeStatus.Running)
                .ToArray();
            nodeStates.CancelUnfinished();
            PublishNodeStates(nodeStates, nodeStatesChanged);
            await EmitNodeCancelledBestEffortAsync(
                trace,
                GetUpdatedStates(nodeStates, unfinished));
            throw;
        }
        catch (Exception exception)
        {
            var snapshot = nodeStates.Snapshot;
            var currentState = snapshot.Single(
                state => string.Equals(
                    state.NodeId,
                    node.Id,
                    StringComparison.Ordinal));
            if (currentState.Status == ExecutionNodeStatus.Running)
            {
                nodeStates.Fail(node.Id);
                var failedState = nodeStates.Snapshot.Single(
                    state => string.Equals(
                        state.NodeId,
                        node.Id,
                        StringComparison.Ordinal));
                var failedData = CreateNodeTraceData(
                    node,
                    mode.Manifest.Name,
                    phase.Name,
                    "failed");
                foreach (var (key, value) in RuntimeFailureFactory.ToTraceData(
                             RuntimeFailureFactory.Create(
                                 exception,
                                 exception is
                                     CognitiveRuntime.Core.Exceptions.ModelProviderException
                                     ? RuntimeFailureCategory.Provider
                                     : RuntimeFailureCategory.Runtime,
                                 phase.Name,
                                 modelClient.ProviderName)))
                {
                    failedData[key] = value;
                }
                AddDuration(failedData, failedState);
                await EmitNodeTerminalBestEffortAsync(
                    trace,
                    TraceEventNames.NodeFailed,
                    failedData);
            }

            var pending = snapshot
                .Where(state => state.Status == ExecutionNodeStatus.Pending)
                .ToArray();
            nodeStates.CancelPending();
            PublishNodeStates(nodeStates, nodeStatesChanged);
            await EmitNodeCancelledBestEffortAsync(
                trace,
                GetUpdatedStates(nodeStates, pending));
            throw;
        }
    }

    private static void PublishNodeStates(
        ExecutionNodeStateTracker nodeStates,
        Action<IReadOnlyList<ExecutionNodeState>>? nodeStatesChanged) =>
        nodeStatesChanged?.Invoke(nodeStates.Snapshot);

    private static Dictionary<string, object?> CreateNodeTraceData(
        PatternExecutionNode node,
        string modeName,
        string phaseName,
        string status) =>
        new()
        {
            [TracePayloadKeys.NodeId] = node.Id,
            [TracePayloadKeys.StageId] = node.StageId,
            [TracePayloadKeys.Mode] = modeName,
            [TracePayloadKeys.Phase] = phaseName,
            [TracePayloadKeys.PhaseKind] = node.PhaseKind.ToString().ToLowerInvariant(),
            [TracePayloadKeys.Status] = status,
            [TracePayloadKeys.Dependencies] = node.DependencyNodeIds,
            [TracePayloadKeys.ContextNodeIds] = node.ContextNodeIds,
            [TracePayloadKeys.InputNodeId] = node.InputNodeId
        };

    private static void AddDuration(
        IDictionary<string, object?> data,
        ExecutionNodeState state)
    {
        if (state.StartedAt is not null && state.EndedAt is not null)
        {
            data[TracePayloadKeys.DurationMs] = RuntimeDuration.GetMilliseconds(
                state.StartedAt.Value,
                state.EndedAt.Value);
        }
    }

    private static IReadOnlyList<ExecutionNodeState> GetUpdatedStates(
        ExecutionNodeStateTracker nodeStates,
        IReadOnlyList<ExecutionNodeState> previousStates)
    {
        var nodeIds = previousStates
            .Select(state => state.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        return nodeStates.Snapshot
            .Where(state => nodeIds.Contains(state.NodeId))
            .ToArray();
    }

    private static async Task EmitNodeCancelledBestEffortAsync(
        ITraceSession trace,
        IReadOnlyList<ExecutionNodeState> states)
    {
        foreach (var state in states)
        {
            var data = new Dictionary<string, object?>
                {
                    [TracePayloadKeys.NodeId] = state.NodeId,
                    [TracePayloadKeys.StageId] = state.StageId,
                    [TracePayloadKeys.Mode] = state.ModeName,
                    [TracePayloadKeys.Phase] = state.PhaseName,
                    [TracePayloadKeys.PhaseKind] =
                        state.PhaseKind.ToString().ToLowerInvariant(),
                    [TracePayloadKeys.Status] = "cancelled"
                };
            AddDuration(data, state);
            await EmitNodeTerminalBestEffortAsync(
                trace,
                TraceEventNames.NodeCancelled,
                data);
        }
    }

    private static async Task EmitNodeTerminalBestEffortAsync(
        ITraceSession trace,
        string eventType,
        IReadOnlyDictionary<string, object?> data)
    {
        try
        {
            await trace.EmitAsync(
                eventType,
                data,
                CancellationToken.None);
        }
        catch
        {
            // The original execution failure or cancellation remains authoritative.
        }
    }

    private static IReadOnlyDictionary<string, LoadedPhase> ResolvePhases(
        PatternExecutionPlan plan,
        IReadOnlyDictionary<string, LoadedMode> loadedModes)
    {
        var phases = new Dictionary<string, LoadedPhase>(StringComparer.Ordinal);
        foreach (var node in plan.Nodes)
        {
            var mode = loadedModes[node.ModeSourceId];
            var phase = mode.Phases.SingleOrDefault(
                candidate => candidate.Kind == node.PhaseKind)
                ?? throw new InvalidOperationException(
                    $"Mode '{mode.Manifest.Name}' does not define phase kind " +
                    $"'{node.PhaseKind}' required by node '{node.Id}'.");
            phases.Add(node.Id, phase);
        }

        return phases;
    }

    private static string ResolveInput(
        string initialInput,
        string? inputNodeId,
        IReadOnlyDictionary<string, PatternNodeExecutionResult> completedResults)
    {
        if (inputNodeId is null)
        {
            return initialInput;
        }

        if (!completedResults.TryGetValue(inputNodeId, out var source))
        {
            throw new InvalidOperationException(
                $"Pattern input node '{inputNodeId}' is failed or incomplete.");
        }

        return source.PhaseResult.Content;
    }
}
