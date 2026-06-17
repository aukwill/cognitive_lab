using System.Collections.Immutable;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

public enum ExecutionNodeStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record ExecutionNodeState(
    string NodeId,
    string? StageId,
    string PhaseName,
    PhaseKind PhaseKind,
    string ModeName,
    ExecutionNodeStatus Status,
    string? Provider = null,
    string? Model = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? EndedAt = null,
    int? OutputLength = null);

internal static class ExecutionNodeStateFactory
{
    public static ImmutableArray<ExecutionNodeState> CreatePending(
        PatternExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var modeSources = plan.ModeSources.ToDictionary(
            source => source.Id,
            StringComparer.Ordinal);
        return plan.Nodes
            .Select(
                node => new ExecutionNodeState(
                    node.Id,
                    node.StageId,
                    node.PhaseKind.ToString().ToLowerInvariant(),
                    node.PhaseKind,
                    modeSources[node.ModeSourceId].ModeName,
                    ExecutionNodeStatus.Pending))
            .ToImmutableArray();
    }
}

internal sealed class ExecutionNodeStateTracker
{
    // Guards all reads and writes so independent nodes can execute concurrently
    // (scatter-gather) without racing on node-state transitions.
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, int> _indexes;
    private readonly ExecutionNodeState[] _states;

    public ExecutionNodeStateTracker(
        PatternExecutionPlan plan,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _states = ExecutionNodeStateFactory.CreatePending(plan).ToArray();
        _indexes = _states
            .Select((state, index) => (state.NodeId, index))
            .ToDictionary(item => item.NodeId, item => item.index, StringComparer.Ordinal);
    }

    public ImmutableArray<ExecutionNodeState> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return ImmutableArray.CreateRange(_states);
            }
        }
    }

    public void Start(
        string nodeId,
        string phaseName,
        string modeName,
        string provider)
    {
        lock (_sync)
        {
            var index = GetIndex(nodeId);
            var current = _states[index];
            RequireStatus(current, ExecutionNodeStatus.Pending);
            _states[index] = current with
            {
                PhaseName = phaseName,
                ModeName = modeName,
                Status = ExecutionNodeStatus.Running,
                Provider = provider,
                StartedAt = _timeProvider.GetUtcNow()
            };
        }
    }

    public void Complete(string nodeId, PhaseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_sync)
        {
            var index = GetIndex(nodeId);
            var current = _states[index];
            RequireStatus(current, ExecutionNodeStatus.Running);
            _states[index] = current with
            {
                Status = ExecutionNodeStatus.Completed,
                Provider = result.Provider,
                Model = result.Model,
                EndedAt = _timeProvider.GetUtcNow(),
                OutputLength = result.Content.Length
            };
        }
    }

    public void Fail(string nodeId)
    {
        lock (_sync)
        {
            var index = GetIndex(nodeId);
            var current = _states[index];
            RequireStatus(current, ExecutionNodeStatus.Running);
            _states[index] = current with
            {
                Status = ExecutionNodeStatus.Failed,
                EndedAt = _timeProvider.GetUtcNow()
            };
        }
    }

    public bool CancelUnfinished()
    {
        lock (_sync)
        {
            var changed = false;
            var endedAt = _timeProvider.GetUtcNow();
            for (var index = 0; index < _states.Length; index++)
            {
                var current = _states[index];
                if (current.Status is not (
                    ExecutionNodeStatus.Pending or ExecutionNodeStatus.Running))
                {
                    continue;
                }

                _states[index] = current with
                {
                    Status = ExecutionNodeStatus.Cancelled,
                    EndedAt = endedAt
                };
                changed = true;
            }

            return changed;
        }
    }

    public bool CancelPending()
    {
        lock (_sync)
        {
            var changed = false;
            var endedAt = _timeProvider.GetUtcNow();
            for (var index = 0; index < _states.Length; index++)
            {
                var current = _states[index];
                if (current.Status != ExecutionNodeStatus.Pending)
                {
                    continue;
                }

                _states[index] = current with
                {
                    Status = ExecutionNodeStatus.Cancelled,
                    EndedAt = endedAt
                };
                changed = true;
            }

            return changed;
        }
    }

    private int GetIndex(string nodeId)
    {
        if (!_indexes.TryGetValue(nodeId, out var index))
        {
            throw new InvalidOperationException(
                $"Pattern node '{nodeId}' has no execution state.");
        }

        return index;
    }

    private static void RequireStatus(
        ExecutionNodeState state,
        ExecutionNodeStatus expected)
    {
        if (state.Status != expected)
        {
            throw new InvalidOperationException(
                $"Pattern node '{state.NodeId}' cannot transition from " +
                $"'{state.Status}' when '{expected}' is required.");
        }
    }
}
