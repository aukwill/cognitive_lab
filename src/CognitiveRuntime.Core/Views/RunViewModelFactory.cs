using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Views;

public static class RunViewModelFactory
{
    private const int OutputSummaryLimit = 500;

    public static RunViewModel Create(
        string runId,
        RunRequest request,
        LoadedMode mode,
        IReadOnlyList<PhaseResult> phaseResults,
        EvalReport evalReport,
        RunArtifactPaths artifacts,
        IReadOnlyList<TraceEvent> traceEvents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(phaseResults);
        ArgumentNullException.ThrowIfNull(evalReport);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(traceEvents);

        var startedAt = FindEventTime(traceEvents, "run.started");
        var endedAt = FindEventTime(traceEvents, "run.finalized")
            ?? traceEvents.LastOrDefault()?.Timestamp;
        var toolPolicyDecisions = CreateToolPolicyDecisions(traceEvents);
        var phaseViews = mode.Phases
            .Select(phase => CreatePhaseView(phase, phaseResults, traceEvents))
            .ToArray();

        return new RunViewModel(
            new RunViewRun(
                runId,
                mode.Manifest.Name,
                request.InputSource ?? "In-memory input",
                request.ModelProvider,
                evalReport.Passed ? "PASS" : "FAIL",
                startedAt,
                endedAt,
                artifacts.RunDirectory),
            CreateArtifactLinks(artifacts),
            new RunViewMode(
                mode.Manifest.Name,
                mode.Manifest.Description,
                mode.Phases.Select(phase => phase.Name).ToArray(),
                "All configured phases complete, followed by deterministic runtime evaluation."),
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
            artifacts.EvalReportPath
        };

        return paths
            .Where(File.Exists)
            .Select(path => new RunViewArtifact(
                Path.GetFileName(path),
                Path.GetRelativePath(artifacts.RunDirectory, path)
                    .Replace(Path.DirectorySeparatorChar, '/')))
            .ToArray();
    }

    private static RunViewPhase CreatePhaseView(
        LoadedPhase phase,
        IReadOnlyList<PhaseResult> phaseResults,
        IReadOnlyList<TraceEvent> traceEvents)
    {
        var result = phaseResults.FirstOrDefault(
            candidate => string.Equals(
                candidate.PhaseName,
                phase.Name,
                StringComparison.OrdinalIgnoreCase));
        var requestedToolCalls = CountPhaseEvents(
            traceEvents,
            "tool.policy_evaluated",
            phase.Name);
        var executedToolCalls = CountPhaseEvents(
            traceEvents,
            "tool.completed",
            phase.Name);

        return new RunViewPhase(
            phase.Name,
            result is null ? "Not completed" : "Completed",
            result?.Provider ?? "Not available",
            phase.Kind == PhaseKind.Revision
                ? "Authoritative result"
                : "Supporting context",
            requestedToolCalls,
            executedToolCalls,
            Summarize(result?.Content));
    }

    private static IReadOnlyList<RunViewToolPolicyDecision> CreateToolPolicyDecisions(
        IReadOnlyList<TraceEvent> traceEvents) =>
        traceEvents
            .Where(traceEvent => string.Equals(
                traceEvent.Type,
                "tool.policy_evaluated",
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
