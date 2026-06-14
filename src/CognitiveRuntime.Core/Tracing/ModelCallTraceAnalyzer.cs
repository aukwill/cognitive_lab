using System.Text.Json;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Tracing;

public sealed record ModelCallTraceEntry(
    string CallId,
    string? NodeId,
    int? Attempt,
    int CalledCount,
    int CompletedCount,
    int FailedCount);

public sealed record ModelCallTraceAnalysis(
    IReadOnlyList<ModelCallTraceEntry> Calls,
    IReadOnlyList<string> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public static class ModelCallTraceAnalyzer
{
    public static ModelCallTraceAnalysis Analyze(
        IReadOnlyList<TraceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var relevantEvents = events
            .Where(traceEvent => traceEvent.Type is
                TraceEventNames.ModelCalled or
                TraceEventNames.ModelCompleted or
                TraceEventNames.ModelFailed)
            .ToArray();
        var issues = new List<string>();
        var groupedEvents = new Dictionary<string, List<TraceEvent>>(
            StringComparer.Ordinal);

        foreach (var traceEvent in relevantEvents)
        {
            var callId = GetDataString(traceEvent, "callId");
            if (string.IsNullOrWhiteSpace(callId))
            {
                issues.Add(
                    $"Trace event '{traceEvent.Type}' at sequence " +
                    $"'{traceEvent.Sequence}' has no call ID.");
                continue;
            }

            if (!groupedEvents.TryGetValue(callId, out var group))
            {
                group = [];
                groupedEvents.Add(callId, group);
            }

            group.Add(traceEvent);
        }

        var calls = groupedEvents
            .OrderBy(pair => pair.Value.Min(traceEvent => traceEvent.Sequence))
            .Select(
                pair => AnalyzeCall(pair.Key, pair.Value, issues))
            .ToArray();

        return new ModelCallTraceAnalysis(calls, issues);
    }

    private static ModelCallTraceEntry AnalyzeCall(
        string callId,
        IReadOnlyList<TraceEvent> events,
        ICollection<string> issues)
    {
        var calledCount = events.Count(
            traceEvent => traceEvent.Type == TraceEventNames.ModelCalled);
        var completedCount = events.Count(
            traceEvent => traceEvent.Type == TraceEventNames.ModelCompleted);
        var failedCount = events.Count(
            traceEvent => traceEvent.Type == TraceEventNames.ModelFailed);
        var terminalCount = completedCount + failedCount;
        var nodeIds = events
            .Select(traceEvent => GetDataString(traceEvent, "nodeId"))
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var attempts = events
            .Select(traceEvent => GetDataInt32(traceEvent, "attempt"))
            .Where(attempt => attempt is not null)
            .Distinct()
            .ToArray();

        if (calledCount != 1)
        {
            issues.Add(
                $"Model call '{callId}' has {calledCount} called events; expected 1.");
        }

        if (terminalCount != 1)
        {
            issues.Add(
                $"Model call '{callId}' has {terminalCount} terminal events; expected 1.");
        }
        else if (calledCount == 1)
        {
            var calledSequence = events.Single(
                traceEvent => traceEvent.Type == TraceEventNames.ModelCalled).Sequence;
            var terminalSequence = events.Single(
                traceEvent => traceEvent.Type is
                    TraceEventNames.ModelCompleted or
                    TraceEventNames.ModelFailed).Sequence;
            if (terminalSequence <= calledSequence)
            {
                issues.Add(
                    $"Model call '{callId}' completed before its called event.");
            }
        }

        if (nodeIds.Length != 1)
        {
            issues.Add(
                $"Model call '{callId}' does not have one consistent node ID.");
        }

        if (attempts.Length != 1)
        {
            issues.Add(
                $"Model call '{callId}' does not have one consistent attempt.");
        }

        return new ModelCallTraceEntry(
            callId,
            nodeIds.SingleOrDefault(),
            attempts.SingleOrDefault(),
            calledCount,
            completedCount,
            failedCount);
    }

    private static string? GetDataString(TraceEvent traceEvent, string key)
    {
        if (!traceEvent.Data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element =>
                element.GetString(),
            JsonElement element => element.ToString(),
            _ => value?.ToString()
        };
    }

    private static int? GetDataInt32(TraceEvent traceEvent, string key)
    {
        if (!traceEvent.Data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            int number => number,
            long number when number is >= int.MinValue and <= int.MaxValue =>
                (int)number,
            JsonElement { ValueKind: JsonValueKind.Number } element
                when element.TryGetInt32(out var number) => number,
            string text when int.TryParse(text, out var number) => number,
            _ => null
        };
    }
}
