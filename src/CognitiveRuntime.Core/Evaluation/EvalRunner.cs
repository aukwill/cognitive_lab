using System.Text;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class EvalRunner : IEvalRunner
{
    private readonly OutputContractValidator _outputContractValidator;
    private readonly LoopEfficacyEvaluator _loopEfficacyEvaluator;

    public EvalRunner(
        OutputContractValidator outputContractValidator,
        LoopEfficacyEvaluator? loopEfficacyEvaluator = null)
    {
        _outputContractValidator = outputContractValidator;
        _loopEfficacyEvaluator = loopEfficacyEvaluator ?? new LoopEfficacyEvaluator();
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
        var main = context.PhaseResults.SingleOrDefault(
            phaseResult => phaseResult.PhaseKind == PhaseKind.Main);
        var critic = context.PhaseResults.SingleOrDefault(
            phaseResult => phaseResult.PhaseKind == PhaseKind.Critic);
        var revision = context.PhaseResults.SingleOrDefault(
            phaseResult => phaseResult.PhaseKind == PhaseKind.Revision);
        var mainContent = main?.Content ?? string.Empty;
        var criticContent = critic?.Content ?? string.Empty;
        var revisionContent = revision?.Content ?? string.Empty;

        var events = context.TraceEvents;
        var checks = new List<EvalCheckResult>
        {
            CheckRequiredArtifacts(context.Artifacts),
            CheckTraceEvent(events, "run.started"),
            CheckTraceEvent(events, "run.completed"),
            CheckTraceEvent(events, "critic.completed", "critic phase ran"),
            CheckTraceEvent(events, "revision.completed", "revision phase ran"),
            new(
                "result is not empty",
                !string.IsNullOrWhiteSpace(result),
                string.IsNullOrWhiteSpace(result)
                    ? "result.md is empty."
                    : "result.md contains content."),
            new(
                "revision is not empty",
                !string.IsNullOrWhiteSpace(revisionContent),
                string.IsNullOrWhiteSpace(revisionContent)
                    ? "The authoritative revision is empty."
                    : "The authoritative revision contains content."),
            _outputContractValidator.Validate(
                revisionContent,
                context.Mode.Manifest.OutputContract),
            _loopEfficacyEvaluator.Evaluate(
                mainContent,
                criticContent,
                revisionContent,
                context.Mode.Manifest.OutputContract)
        };

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

    private static EvalCheckResult CheckRequiredArtifacts(
        RunArtifactPaths artifacts)
    {
        var missing = artifacts.RequiredPaths
            .Where(path => !File.Exists(path))
            .Select(Path.GetFileName)
            .ToArray();

        return new EvalCheckResult(
            "required artifacts exist",
            missing.Length == 0,
            missing.Length == 0
                ? "All required artifact paths exist."
                : $"Missing artifacts: {string.Join(", ", missing)}.");
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
}
