using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime;

public enum RunLifecycleStatus
{
    Created,
    Running,
    ExecutionCompleted,
    Evaluating,
    Finalizing,
    Succeeded,
    Failed,
    Cancelled
}

public sealed record RunLifecycleTransition(
    RunLifecycleStatus From,
    RunLifecycleStatus To);

internal static class RunLifecycleStateMachine
{
    public static RunState Transition(
        RunState state,
        RunLifecycleStatus target,
        RunOutcome? outcome = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var current = state.LifecycleStatus;
        if (IsTerminal(current))
        {
            throw new InvalidOperationException(
                $"Run lifecycle state '{current}' is terminal and cannot transition.");
        }

        if (!IsAllowed(current, target))
        {
            throw new InvalidOperationException(
                $"Run lifecycle cannot transition from '{current}' to '{target}'.");
        }

        ValidateOutcome(target, outcome);
        return state with
        {
            LifecycleStatus = target,
            Outcome = outcome,
            LastTransition = new RunLifecycleTransition(current, target)
        };
    }

    public static bool IsTerminal(RunLifecycleStatus status) =>
        status is RunLifecycleStatus.Succeeded
            or RunLifecycleStatus.Failed
            or RunLifecycleStatus.Cancelled;

    private static bool IsAllowed(
        RunLifecycleStatus current,
        RunLifecycleStatus target) =>
        target switch
        {
            RunLifecycleStatus.Running =>
                current == RunLifecycleStatus.Created,
            RunLifecycleStatus.ExecutionCompleted =>
                current == RunLifecycleStatus.Running,
            RunLifecycleStatus.Evaluating =>
                current == RunLifecycleStatus.ExecutionCompleted,
            RunLifecycleStatus.Finalizing =>
                current == RunLifecycleStatus.Evaluating,
            RunLifecycleStatus.Succeeded =>
                current == RunLifecycleStatus.Finalizing,
            RunLifecycleStatus.Failed or RunLifecycleStatus.Cancelled =>
                !IsTerminal(current),
            _ => false
        };

    private static void ValidateOutcome(
        RunLifecycleStatus target,
        RunOutcome? outcome)
    {
        var valid = target switch
        {
            RunLifecycleStatus.Succeeded =>
                outcome is RunOutcome.Success or RunOutcome.EvalFailed,
            RunLifecycleStatus.Failed =>
                outcome == RunOutcome.RuntimeFailed,
            RunLifecycleStatus.Cancelled =>
                outcome == RunOutcome.Cancelled,
            _ => outcome is null
        };

        if (!valid)
        {
            throw new InvalidOperationException(
                $"Run lifecycle target '{target}' does not accept outcome " +
                $"'{outcome?.ToString() ?? "none"}'.");
        }
    }
}

internal sealed record RunTerminalTraceEvent(
    string EventType,
    IReadOnlyDictionary<string, object?> Data);

internal static class RunTerminalTraceEventFactory
{
    public static RunTerminalTraceEvent Create(
        RunState state,
        IReadOnlyDictionary<string, object?>? additionalData = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!RunLifecycleStateMachine.IsTerminal(state.LifecycleStatus) ||
            state.Outcome is null ||
            state.LastTransition is null ||
            state.LastTransition.To != state.LifecycleStatus)
        {
            throw new InvalidOperationException(
                "A terminal trace event requires a validated terminal lifecycle transition.");
        }

        var eventType = state.LifecycleStatus switch
        {
            RunLifecycleStatus.Succeeded => TraceEventNames.RunFinalized,
            RunLifecycleStatus.Failed => TraceEventNames.RunFailed,
            RunLifecycleStatus.Cancelled => TraceEventNames.RunCancelled,
            _ => throw new InvalidOperationException(
                $"Lifecycle state '{state.LifecycleStatus}' is not terminal.")
        };
        var data = new Dictionary<string, object?>
        {
            [TracePayloadKeys.FromStatus] = ToTraceValue(state.LastTransition.From),
            [TracePayloadKeys.LifecycleStatus] = ToTraceValue(state.LifecycleStatus),
            [TracePayloadKeys.Outcome] = ToTraceValue(state.Outcome.Value)
        };

        if (additionalData is not null)
        {
            foreach (var (key, value) in additionalData)
            {
                data[key] = value;
            }
        }

        return new RunTerminalTraceEvent(eventType, data);
    }

    private static string ToTraceValue<T>(T value)
        where T : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
