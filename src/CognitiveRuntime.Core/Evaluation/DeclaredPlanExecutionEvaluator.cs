using System.Collections;
using System.Text.Json;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class DeclaredPlanExecutionEvaluator
{
    private static readonly HashSet<string> TerminalEventTypes =
    [
        TraceEventNames.NodeCompleted,
        TraceEventNames.NodeFailed,
        TraceEventNames.NodeCancelled
    ];

    public EvalCheckResult Evaluate(
        EvalExecutionPlan plan,
        IReadOnlyList<TraceEvent> events,
        IReadOnlyDictionary<string, PhaseResult>? nodeResultsById,
        string? authoritativeContent)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(events);

        var issues = new List<string>();
        var declaredIds = plan.Nodes
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var startedByNodeId = GroupByNodeId(
            events.Where(traceEvent =>
                traceEvent.Type == TraceEventNames.NodeStarted),
            issues);
        var terminalByNodeId = GroupByNodeId(
            events.Where(traceEvent =>
                TerminalEventTypes.Contains(traceEvent.Type)),
            issues);

        AddUndeclaredIssues(startedByNodeId.Keys, declaredIds, "started", issues);
        AddUndeclaredIssues(terminalByNodeId.Keys, declaredIds, "terminal", issues);

        foreach (var node in plan.Nodes)
        {
            var started = GetEvents(startedByNodeId, node.NodeId);
            var terminal = GetEvents(terminalByNodeId, node.NodeId);
            if (started.Count != 1)
            {
                issues.Add(
                    $"Node '{node.NodeId}' has {started.Count} started events; expected 1.");
            }

            if (terminal.Count != 1)
            {
                issues.Add(
                    $"Node '{node.NodeId}' has {terminal.Count} terminal events; expected 1.");
                continue;
            }

            var terminalEvent = terminal[0];
            if (terminalEvent.Type != TraceEventNames.NodeCompleted)
            {
                issues.Add(
                    $"Node '{node.NodeId}' ended with '{terminalEvent.Type}', " +
                    $"not '{TraceEventNames.NodeCompleted}'.");
            }

            var tracedContext = GetDataStrings(
                terminalEvent,
                TracePayloadKeys.ContextNodeIds);
            if (!node.ContextNodeIds.SequenceEqual(
                    tracedContext,
                    StringComparer.Ordinal))
            {
                issues.Add(
                    $"Node '{node.NodeId}' context was " +
                    $"[{string.Join(", ", tracedContext)}]; expected " +
                    $"[{string.Join(", ", node.ContextNodeIds)}].");
            }

            if (started.Count == 1)
            {
                foreach (var dependencyId in node.DependencyNodeIds)
                {
                    var dependencyTerminal = GetEvents(
                        terminalByNodeId,
                        dependencyId);
                    if (dependencyTerminal.Count == 1 &&
                        dependencyTerminal[0].Sequence >= started[0].Sequence)
                    {
                        issues.Add(
                            $"Node '{node.NodeId}' started before dependency " +
                            $"'{dependencyId}' completed.");
                    }
                }
            }
        }

        CheckStageOrdering(plan, startedByNodeId, terminalByNodeId, issues);
        CheckResults(
            plan,
            nodeResultsById,
            authoritativeContent,
            terminalByNodeId,
            issues);

        return new EvalCheckResult(
            "declared plan execution",
            issues.Count == 0,
            issues.Count == 0
                ? $"All {plan.Nodes.Count} declared node(s) executed exactly once " +
                  "with valid context and ordering."
                : string.Join(" ", issues));
    }

    private static void CheckResults(
        EvalExecutionPlan plan,
        IReadOnlyDictionary<string, PhaseResult>? nodeResultsById,
        string? authoritativeContent,
        IReadOnlyDictionary<string, List<TraceEvent>> terminalByNodeId,
        ICollection<string> issues)
    {
        if (nodeResultsById is null)
        {
            issues.Add("Execution node results were not supplied to evaluation.");
            return;
        }

        var declaredIds = plan.Nodes
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var nodeId in declaredIds)
        {
            if (!nodeResultsById.ContainsKey(nodeId))
            {
                issues.Add($"Node '{nodeId}' has no execution result.");
            }
        }

        foreach (var nodeId in nodeResultsById.Keys)
        {
            if (!declaredIds.Contains(nodeId))
            {
                issues.Add($"Undeclared node '{nodeId}' has an execution result.");
            }
        }

        if (!nodeResultsById.TryGetValue(
                plan.AuthoritativeNodeId,
                out var authoritativeResult))
        {
            issues.Add(
                $"Authoritative node '{plan.AuthoritativeNodeId}' has no result.");
            return;
        }

        var authoritativeTerminals = GetEvents(
            terminalByNodeId,
            plan.AuthoritativeNodeId);
        if (authoritativeTerminals.Count != 1 ||
            authoritativeTerminals[0].Type != TraceEventNames.NodeCompleted)
        {
            issues.Add(
                $"Authoritative node '{plan.AuthoritativeNodeId}' did not " +
                "complete successfully.");
        }

        if (!string.Equals(
                authoritativeResult.Content,
                authoritativeContent,
                StringComparison.Ordinal))
        {
            issues.Add(
                $"Authoritative node '{plan.AuthoritativeNodeId}' does not " +
                "match the content used for output-contract evaluation.");
        }
    }

    private static void CheckStageOrdering(
        EvalExecutionPlan plan,
        IReadOnlyDictionary<string, List<TraceEvent>> startedByNodeId,
        IReadOnlyDictionary<string, List<TraceEvent>> terminalByNodeId,
        ICollection<string> issues)
    {
        var stageIds = plan.Nodes
            .Select(node => node.StageId)
            .Where(stageId => !string.IsNullOrWhiteSpace(stageId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        for (var index = 1; index < stageIds.Length; index++)
        {
            var previousNodeIds = plan.Nodes
                .Where(node => node.StageId == stageIds[index - 1])
                .Select(node => node.NodeId)
                .ToArray();
            var currentNodeIds = plan.Nodes
                .Where(node => node.StageId == stageIds[index])
                .Select(node => node.NodeId)
                .ToArray();
            var previousTerminalSequences = previousNodeIds
                .SelectMany(nodeId => GetEvents(terminalByNodeId, nodeId))
                .Select(traceEvent => traceEvent.Sequence)
                .ToArray();
            var currentStartedSequences = currentNodeIds
                .SelectMany(nodeId => GetEvents(startedByNodeId, nodeId))
                .Select(traceEvent => traceEvent.Sequence)
                .ToArray();

            if (previousTerminalSequences.Length > 0 &&
                currentStartedSequences.Length > 0 &&
                previousTerminalSequences.Max() >= currentStartedSequences.Min())
            {
                issues.Add(
                    $"Stage '{stageIds[index]}' started before stage " +
                    $"'{stageIds[index - 1]}' finished.");
            }
        }
    }

    private static Dictionary<string, List<TraceEvent>> GroupByNodeId(
        IEnumerable<TraceEvent> events,
        ICollection<string> issues)
    {
        var result = new Dictionary<string, List<TraceEvent>>(
            StringComparer.Ordinal);
        foreach (var traceEvent in events)
        {
            var nodeId = GetDataString(traceEvent, TracePayloadKeys.NodeId);
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                issues.Add(
                    $"Trace event '{traceEvent.Type}' at sequence " +
                    $"'{traceEvent.Sequence}' has no node ID.");
                continue;
            }

            if (!result.TryGetValue(nodeId, out var group))
            {
                group = [];
                result.Add(nodeId, group);
            }

            group.Add(traceEvent);
        }

        return result;
    }

    private static void AddUndeclaredIssues(
        IEnumerable<string> actualIds,
        IReadOnlySet<string> declaredIds,
        string eventKind,
        ICollection<string> issues)
    {
        foreach (var nodeId in actualIds.Where(
                     nodeId => !declaredIds.Contains(nodeId)))
        {
            issues.Add(
                $"Undeclared node '{nodeId}' has a {eventKind} event.");
        }
    }

    private static IReadOnlyList<TraceEvent> GetEvents(
        IReadOnlyDictionary<string, List<TraceEvent>> eventsByNodeId,
        string nodeId) =>
        eventsByNodeId.TryGetValue(nodeId, out var events)
            ? events
            : [];

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

    private static IReadOnlyList<string> GetDataStrings(
        TraceEvent traceEvent,
        string key)
    {
        if (!traceEvent.Data.TryGetValue(key, out var value) ||
            value is null)
        {
            return [];
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            return element.EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();
        }

        if (value is IEnumerable<string> strings)
        {
            return strings.ToArray();
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .ToArray();
        }

        return [];
    }
}
