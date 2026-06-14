using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Core.Views;

public static class RunViewModelFactory
{
    private const int OutputSummaryLimit = 500;

    public static RunViewModel Create(
        RunRequest request,
        RunState state,
        IReadOnlyList<TraceEvent> traceEvents)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(traceEvents);

        var execution = state.Execution
            ?? throw new InvalidOperationException(
                "Run execution must complete before creating a run view.");
        var evalReport = state.EvalReport
            ?? throw new InvalidOperationException(
                "Run evaluation must complete before creating a run view.");
        var artifacts = state.Artifacts;
        var startedAt = FindEventTime(
            traceEvents,
            TraceEventNames.RunStarted);
        var endedAt = FindEventTime(
                traceEvents,
                TraceEventNames.RunFinalized)
            ?? traceEvents.LastOrDefault()?.Timestamp;
        var toolPolicyDecisions = CreateToolPolicyDecisions(traceEvents);
        var isPipeline = execution.Stages.Count > 0;
        var mode = execution.AuthoritativeMode;
        var sequenceNames = isPipeline
            ? execution.Stages.Select(stage => stage.ModeName).ToArray()
            : execution.NodeResults.Select(result => result.Phase.Name).ToArray();
        var phaseViews = isPipeline
            ? CreatePipelinePhaseViews(execution.Stages)
            : execution.NodeResults
                .Select(result => CreatePhaseView(result, traceEvents))
                .ToArray();

        return new RunViewModel(
            new RunViewRun(
                state.RunId,
                isPipeline ? request.ModeName : mode.Manifest.Name,
                request.InputSource ?? "In-memory input",
                request.ModelProvider,
                evalReport.Passed ? "PASS" : "FAIL",
                startedAt,
                endedAt,
                artifacts.RunDirectory),
            isPipeline
                ? CreatePipelineArtifactLinks(artifacts, execution.Stages)
                : CreateArtifactLinks(artifacts),
            CreatePattern(execution),
            new RunViewMode(
                isPipeline ? request.ModeName : mode.Manifest.Name,
                isPipeline
                    ? $"Ordered pipeline of {execution.Stages.Count} runtime-owned mode stages."
                    : mode.Manifest.Description,
                isPipeline ? "Configured stages" : "Configured phases",
                sequenceNames,
                isPipeline
                    ? "All configured stages complete in order, followed by deterministic runtime evaluation."
                    : "All planned phases complete, followed by deterministic runtime evaluation."),
            phaseViews,
            toolPolicyDecisions,
            new RunViewEval(evalReport.Passed, evalReport.Checks),
            new RunViewTrace(
                Path.GetFileName(artifacts.TracePath),
                traceEvents.Count));
    }

    private static IReadOnlyList<RunViewArtifact> CreateArtifactLinks(
        RunArtifactPaths artifacts)
    {
        var paths = new[]
        {
            artifacts.InputPath,
            artifacts.ResultPath,
            artifacts.TracePath,
            artifacts.RunSummaryPath,
            artifacts.EvalReportPath,
            artifacts.PatternPath
        };

        return paths
            .Where(File.Exists)
            .Select(path => new RunViewArtifact(
                Path.GetFileName(path),
                Path.GetRelativePath(artifacts.RunDirectory, path)
                    .Replace(Path.DirectorySeparatorChar, '/')))
            .ToArray();
    }

    private static IReadOnlyList<RunViewArtifact> CreatePipelineArtifactLinks(
        RunArtifactPaths artifacts,
        IReadOnlyList<PipelineStageResult> stages)
    {
        var links = CreateArtifactLinks(artifacts).ToList();

        foreach (var stage in stages)
        {
            AddArtifactLink(
                links,
                artifacts.RunDirectory,
                $"Stage {stage.StageIndex:D2} {stage.ModeName} input",
                Path.Combine(stage.StageDirectory, "input.md"));
            AddArtifactLink(
                links,
                artifacts.RunDirectory,
                $"Stage {stage.StageIndex:D2} {stage.ModeName} result",
                Path.Combine(stage.StageDirectory, "result.md"));
        }

        return links;
    }

    private static void AddArtifactLink(
        ICollection<RunViewArtifact> links,
        string runDirectory,
        string name,
        string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        links.Add(
            new RunViewArtifact(
                name,
                Path.GetRelativePath(runDirectory, path)
                    .Replace(Path.DirectorySeparatorChar, '/')));
    }

    private static RunViewPattern CreatePattern(PatternExecutionResult execution) =>
        execution.Stages.Count == 0
            ? CreateNodePattern(execution)
            : CreatePipelinePattern(execution);

    private static RunViewPattern CreateNodePattern(
        PatternExecutionResult execution)
    {
        var nodes = execution.NodeResults
            .Select(
                (result, index) => new RunViewPatternNode(
                    result.Node.Id,
                    result.Phase.Name,
                    $"Step {index + 1} - {result.Phase.Kind.ToString().ToLowerInvariant()}",
                    result.ContextNodeIds.Count == 0
                        ? "Receives the run input with no prior phase context."
                        : $"Receives context from {string.Join(", ", result.ContextNodeIds)}."))
            .ToArray();
        var edges = execution.NodeResults
            .SelectMany(
                result => result.ContextNodeIds.Select(
                    contextNodeId => new RunViewPatternEdge(
                        contextNodeId,
                        result.Node.Id,
                        "provides context")))
            .ToArray();

        return new RunViewPattern(execution.Plan.PatternName, "step", nodes, edges);
    }

    private static RunViewPattern CreatePipelinePattern(
        PatternExecutionResult execution)
    {
        var nodes = execution.Stages
            .Select(
                stage => new RunViewPatternNode(
                    $"stage-{stage.StageIndex}",
                    stage.ModeName,
                    $"Stage {stage.StageIndex:D2}",
                    stage.StageIndex == 1
                        ? "Receives the run's initial input."
                        : $"Receives stage {stage.StageIndex - 1:D2}'s authoritative revision."))
            .ToArray();
        var edges = nodes
            .Skip(1)
            .Select(
                (node, index) => new RunViewPatternEdge(
                    nodes[index].Id,
                    node.Id,
                    "authoritative revision"))
            .ToArray();

        return new RunViewPattern(execution.Plan.PatternName, "stage", nodes, edges);
    }

    private static IReadOnlyList<RunViewPhase> CreatePipelinePhaseViews(
        IReadOnlyList<PipelineStageResult> stages) =>
        stages
            .SelectMany(
                stage => stage.PhaseResults.Select(
                    result => new RunViewPhase(
                        $"Stage {stage.StageIndex:D2} / {stage.ModeName} / {result.PhaseName}",
                        "Completed",
                        result.Provider,
                        GetPhaseRole(result.PhaseKind),
                        0,
                        0,
                        Summarize(result.Content))))
            .ToArray();

    private static RunViewPhase CreatePhaseView(
        PatternNodeExecutionResult nodeResult,
        IReadOnlyList<TraceEvent> traceEvents)
    {
        var phase = nodeResult.Phase;
        var result = nodeResult.PhaseResult;
        var requestedToolCalls = CountPhaseEvents(
            traceEvents,
            TraceEventNames.ToolPolicyEvaluated,
            phase.Name);
        var executedToolCalls = CountPhaseEvents(
            traceEvents,
            TraceEventNames.ToolCompleted,
            phase.Name);

        return new RunViewPhase(
            phase.Name,
            "Completed",
            result.Provider,
            GetPhaseRole(phase.Kind),
            requestedToolCalls,
            executedToolCalls,
            Summarize(result.Content));
    }

    private static string GetPhaseRole(PhaseKind phaseKind) =>
        phaseKind == PhaseKind.Revision
            ? "Authoritative result"
            : "Supporting context";

    private static IReadOnlyList<RunViewToolPolicyDecision> CreateToolPolicyDecisions(
        IReadOnlyList<TraceEvent> traceEvents) =>
        traceEvents
            .Where(traceEvent => string.Equals(
                traceEvent.Type,
                TraceEventNames.ToolPolicyEvaluated,
                StringComparison.Ordinal))
            .Select(traceEvent => new RunViewToolPolicyDecision(
                GetDataString(traceEvent, "tool") ?? "Unknown",
                GetDataString(traceEvent, "category") ?? "Unknown",
                GetDataBoolean(traceEvent, "allowed") ? "Allowed" : "Blocked",
                GetDataString(traceEvent, "reason") ?? "No reason recorded.",
                GetDataString(traceEvent, "phase") ?? "Not recorded"))
            .ToArray();

    private static int CountPhaseEvents(
        IReadOnlyList<TraceEvent> traceEvents,
        string eventType,
        string phaseName) =>
        traceEvents.Count(traceEvent =>
            string.Equals(traceEvent.Type, eventType, StringComparison.Ordinal) &&
            string.Equals(
                GetDataString(traceEvent, "phase"),
                phaseName,
                StringComparison.OrdinalIgnoreCase));

    private static DateTimeOffset? FindEventTime(
        IReadOnlyList<TraceEvent> traceEvents,
        string eventType) =>
        traceEvents
            .LastOrDefault(traceEvent => string.Equals(
                traceEvent.Type,
                eventType,
                StringComparison.Ordinal))
            ?.Timestamp;

    private static string? GetDataString(TraceEvent traceEvent, string key) =>
        traceEvent.Data.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;

    private static bool GetDataBoolean(TraceEvent traceEvent, string key) =>
        traceEvent.Data.TryGetValue(key, out var value) &&
        value switch
        {
            bool boolean => boolean,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false
        };

    private static string Summarize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "No output was produced.";
        }

        var normalized = content.Trim().ReplaceLineEndings(" ");
        return normalized.Length <= OutputSummaryLimit
            ? normalized
            : string.Concat(
                normalized.AsSpan(0, OutputSummaryLimit - 3),
                "...");
    }
}
