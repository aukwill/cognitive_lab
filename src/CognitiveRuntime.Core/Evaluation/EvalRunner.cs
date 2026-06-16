using System.Text;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class EvalRunner : IEvalRunner
{
    private readonly OutputContractValidator _outputContractValidator;
    private readonly LoopEfficacyEvaluator _loopEfficacyEvaluator;
    private readonly DeclaredPlanExecutionEvaluator _planExecutionEvaluator;
    private readonly ModelCallPairingEvaluator _modelCallPairingEvaluator;

    public EvalRunner(
        OutputContractValidator outputContractValidator,
        LoopEfficacyEvaluator? loopEfficacyEvaluator = null,
        DeclaredPlanExecutionEvaluator? planExecutionEvaluator = null,
        ModelCallPairingEvaluator? modelCallPairingEvaluator = null)
    {
        _outputContractValidator = outputContractValidator;
        _loopEfficacyEvaluator = loopEfficacyEvaluator ?? new LoopEfficacyEvaluator();
        _planExecutionEvaluator =
            planExecutionEvaluator ?? new DeclaredPlanExecutionEvaluator();
        _modelCallPairingEvaluator =
            modelCallPairingEvaluator ?? new ModelCallPairingEvaluator();
    }

    public async Task<EvalReport> EvaluateAsync(
        EvalContext context,
        CancellationToken cancellationToken = default)
    {
        var result = File.Exists(context.Artifacts.ResultPath)
            ? await File.ReadAllTextAsync(
                context.Artifacts.ResultPath,
                cancellationToken)
            : string.Empty;
        var main = context.PhaseResults.FirstOrDefault(
            phaseResult => phaseResult.PhaseKind == PhaseKind.Main);
        var critic = context.PhaseResults.FirstOrDefault(
            phaseResult => phaseResult.PhaseKind == PhaseKind.Critic);
        var revision = context.PhaseResults.FirstOrDefault(
            phaseResult => phaseResult.PhaseKind == PhaseKind.Revision);
        var authoritative = context.PhaseResults.FirstOrDefault(
            phaseResult =>
                phaseResult.PhaseKind == context.Plan.AuthoritativePhaseKind);
        var mainContent = main?.Content ?? string.Empty;
        var criticContent = critic?.Content ?? string.Empty;
        var revisionContent = revision?.Content ?? string.Empty;
        var authoritativeContent = authoritative?.Content ?? string.Empty;

        var events = context.TraceEvents;
        var checks = new List<EvalCheckResult>
        {
            CheckRequiredArtifacts(context),
            CheckTraceEvent(events, TraceEventNames.RunStarted),
            CheckTraceEvent(events, TraceEventNames.RunCompleted),
            new(
                "result is not empty",
                !string.IsNullOrWhiteSpace(result),
                string.IsNullOrWhiteSpace(result)
                    ? "result.md is empty."
                    : "result.md contains content.")
        };

        checks.AddRange(
            context.Plan.RequiredPhaseKinds.Select(
                phaseKind => CheckPhaseCompleted(
                    context.PhaseResults,
                    events,
                    phaseKind)));
        checks.Add(
            CheckAuthoritativeOutput(
                context.Plan.AuthoritativePhaseKind,
                authoritativeContent));
        checks.Add(
            _outputContractValidator.Validate(
                authoritativeContent,
                context.Mode.Manifest.OutputContract));

        if (context.Plan.Execution is not null)
        {
            checks.Add(
                _planExecutionEvaluator.Evaluate(
                    context.Plan.Execution,
                    events,
                    context.NodeResultsById,
                    context.AuthoritativeContent));
            checks.Add(_modelCallPairingEvaluator.Evaluate(events));
        }

        if (context.Plan.EvaluateLoopEfficacy)
        {
            checks.Add(
                _loopEfficacyEvaluator.Evaluate(
                    mainContent,
                    criticContent,
                    revisionContent,
                    context.Mode.Manifest.OutputContract));
        }

        return new EvalReport(checks);
    }

    public static string RenderMarkdown(EvalReport report)
    {
        var builder = new StringBuilder()
            .AppendLine("# Evaluation Report")
            .AppendLine()
            .AppendLine($"Overall: **{(report.Passed ? "PASS" : "FAIL")}**")
            .AppendLine();

        foreach (var check in report.Checks)
        {
            builder
                .Append("- [")
                .Append(check.Passed ? 'x' : ' ')
                .Append("] ")
                .Append(check.Name)
                .Append(": ")
                .AppendLine(check.Details);
        }

        return builder.ToString();
    }

    private static EvalCheckResult CheckRequiredArtifacts(EvalContext context)
    {
        var ledger = context.ArtifactLedger;
        if (ledger is null || ledger.Count == 0)
        {
            // No artifact ledger supplied (for example a direct eval): fall back
            // to verifying the required artifact files exist on disk.
            var missingFiles = context.Artifacts.RequiredPaths
                .Where(path => !File.Exists(path))
                .Select(Path.GetFileName)
                .ToArray();

            return new EvalCheckResult(
                "required artifacts exist",
                missingFiles.Length == 0,
                missingFiles.Length == 0
                    ? "All required artifact paths exist."
                    : $"Missing artifacts: {string.Join(", ", missingFiles)}.");
        }

        // The runtime tracks artifact planning and completion explicitly, so the
        // check no longer depends on a placeholder eval report existing on disk.
        // It fails when an artifact's write was attempted and failed, or when an
        // artifact reported as written is missing on disk. Planned-but-pending
        // artifacts (the eval report and run manifest, produced after this check)
        // are expected and do not fail it.
        var failed = ledger
            .Where(artifact => artifact.Status == RunArtifactStatus.Failed)
            .Select(artifact => artifact.Name)
            .ToArray();
        var writtenButMissing = ledger
            .Where(artifact =>
                artifact.Status == RunArtifactStatus.Written &&
                !File.Exists(
                    Path.Combine(context.Artifacts.RunDirectory, artifact.Name)))
            .Select(artifact => artifact.Name)
            .ToArray();
        var problems = failed.Concat(writtenButMissing).ToArray();

        return new EvalCheckResult(
            "required artifacts exist",
            problems.Length == 0,
            problems.Length == 0
                ? "All written required artifacts exist; none failed."
                : $"Failed or missing artifacts: {string.Join(", ", problems)}.");
    }

    private static EvalCheckResult CheckTraceEvent(
        IReadOnlyList<TraceEvent> events,
        string eventType,
        string? checkName = null)
    {
        var found = events.Any(
            traceEvent => string.Equals(
                traceEvent.Type,
                eventType,
                StringComparison.Ordinal));

        return new EvalCheckResult(
            checkName ?? $"trace contains {eventType}",
            found,
            found
                ? $"Trace contains '{eventType}'."
                : $"Trace does not contain '{eventType}'.");
    }

    private static EvalCheckResult CheckPhaseCompleted(
        IReadOnlyList<PhaseResult> phaseResults,
        IReadOnlyList<TraceEvent> events,
        PhaseKind phaseKind)
    {
        var matches = phaseResults
            .Where(result => result.PhaseKind == phaseKind)
            .ToArray();
        var phaseName = matches.FirstOrDefault()?.PhaseName;
        var completionEventType = phaseKind switch
        {
            PhaseKind.Main => TraceEventNames.ModelCompleted,
            PhaseKind.Critic => TraceEventNames.CriticCompleted,
            PhaseKind.Revision => TraceEventNames.RevisionCompleted,
            _ => throw new ArgumentOutOfRangeException(
                nameof(phaseKind),
                phaseKind,
                "Unsupported phase kind.")
        };
        var completionEventFound = phaseName is not null &&
            events.Any(traceEvent =>
                string.Equals(
                    traceEvent.Type,
                    completionEventType,
                    StringComparison.Ordinal) &&
                string.Equals(
                    GetDataString(traceEvent, "phase"),
                    phaseName,
                    StringComparison.OrdinalIgnoreCase));
        var passed = matches.Length == 1 && completionEventFound;
        var kindName = phaseKind.ToString().ToLowerInvariant();
        var details = matches.Length switch
        {
            0 => $"No {kindName} phase result was recorded.",
            > 1 => $"Expected one {kindName} phase result, but found {matches.Length}.",
            _ when !completionEventFound =>
                $"Trace does not contain '{completionEventType}' for phase '{phaseName}'.",
            _ => $"The {kindName} phase completed exactly once."
        };

        return new EvalCheckResult(
            $"{kindName} phase ran",
            passed,
            details);
    }

    private static EvalCheckResult CheckAuthoritativeOutput(
        PhaseKind authoritativePhaseKind,
        string content)
    {
        var kindName = authoritativePhaseKind.ToString().ToLowerInvariant();
        var passed = !string.IsNullOrWhiteSpace(content);

        return new EvalCheckResult(
            $"{kindName} is not empty",
            passed,
            passed
                ? $"The authoritative {kindName} contains content."
                : $"The authoritative {kindName} is empty.");
    }

    private static string? GetDataString(TraceEvent traceEvent, string key) =>
        traceEvent.Data.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
}
