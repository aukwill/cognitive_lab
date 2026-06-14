using System.Text;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Evaluation;

public sealed class DistillationEvaluator : IDistillationEvaluator
{
    private static readonly string[] RequiredHeadings =
    [
        "## Research Question",
        "## Executive Summary",
        "## Central Pillars",
        "## Cross-Cutting Themes",
        "## Tensions and Contradictions",
        "## Evidence Gaps",
        "## Evidence Quality",
        "## Conclusion",
        "## Sources"
    ];

    public Task<EvalReport> EvaluateAsync(
        EvalContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var evidenceIds = context.Chunks
            .Select(chunk => chunk.Id)
            .ToHashSet(StringComparer.Ordinal);
        var claims = context.Analysis.Pillars
            .SelectMany(pillar => pillar.Claims)
            .ToArray();
        var citedIds = claims
            .SelectMany(claim => claim.EvidenceIds)
            .ToArray();
        var citedSourceIds = citedIds
            .Select(id => id.Split("-C", StringSplitOptions.None)[0])
            .Distinct(StringComparer.Ordinal)
            .Count();
        var expectedSourceCoverage = Math.Min(2, context.Documents.Count);
        var missingHeadings = RequiredHeadings
            .Where(
                heading => !context.Report.Contains(
                    heading,
                    StringComparison.Ordinal))
            .ToArray();
        var missingEvidence = citedIds
            .Where(id => !evidenceIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var duplicateClaimIds = claims
            .GroupBy(claim => claim.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        var invalidStances = claims
            .Where(claim => !ClaimStances.All.Contains(claim.Stance))
            .Select(claim => claim.Id)
            .ToArray();
        var invalidConfidence = claims
            .Where(claim => claim.Confidence is < 0 or > 1)
            .Select(claim => claim.Id)
            .ToArray();
        var weakCorroboration = context.EvidenceMatrix.Claims
            .Where(
                claim =>
                    claim.Stance == ClaimStances.Corroborated &&
                    claim.SourceIds.Count < 2)
            .Select(claim => claim.ClaimId)
            .ToArray();

        var checks = new List<EvalCheck>
        {
            CheckRequiredArtifacts(context.Artifacts),
            CheckTraceEvent(context.TraceEvents, "run.started"),
            CheckTraceEvent(context.TraceEvents, "critic.completed"),
            CheckTraceEvent(context.TraceEvents, "revision.completed"),
            CheckTraceEvent(context.TraceEvents, "source_risk.scanned"),
            CheckTraceEvent(context.TraceEvents, "evidence_matrix.completed"),
            CheckTraceEvent(context.TraceEvents, "run.completed"),
            new(
                "topic was inferred",
                !string.IsNullOrWhiteSpace(context.Analysis.Topic),
                string.IsNullOrWhiteSpace(context.Analysis.Topic)
                    ? "The inferred topic is empty."
                    : $"Inferred topic: {context.Analysis.Topic}."),
            new(
                "at least two pillars exist",
                context.Analysis.Pillars.Length >= 2,
                $"Found {context.Analysis.Pillars.Length} pillars."),
            new(
                "atomic claims exist",
                claims.Length >= context.Analysis.Pillars.Length,
                $"Found {claims.Length} atomic claims across " +
                $"{context.Analysis.Pillars.Length} pillars."),
            new(
                "claim IDs are unique",
                duplicateClaimIds.Length == 0,
                duplicateClaimIds.Length == 0
                    ? "Every claim ID is unique."
                    : $"Duplicate claim IDs: {string.Join(", ", duplicateClaimIds)}."),
            new(
                "every claim cites evidence",
                claims.All(claim => claim.EvidenceIds.Length > 0),
                "Each atomic claim must cite at least one evidence chunk."),
            new(
                "all citations resolve",
                missingEvidence.Length == 0,
                missingEvidence.Length == 0
                    ? "Every citation resolves to evidence.json."
                    : $"Unknown evidence IDs: {string.Join(", ", missingEvidence)}."),
            new(
                "claim stances are valid",
                invalidStances.Length == 0,
                invalidStances.Length == 0
                    ? "Every claim uses a declared stance."
                    : $"Claims with invalid stances: {string.Join(", ", invalidStances)}."),
            new(
                "claim confidence is bounded",
                invalidConfidence.Length == 0,
                invalidConfidence.Length == 0
                    ? "Every confidence value is between 0 and 1."
                    : $"Claims with invalid confidence: " +
                      $"{string.Join(", ", invalidConfidence)}."),
            new(
                "corroborated claims use multiple sources",
                weakCorroboration.Length == 0,
                weakCorroboration.Length == 0
                    ? "Every corroborated claim cites at least two source documents."
                    : $"Under-supported corroborated claims: " +
                      $"{string.Join(", ", weakCorroboration)}."),
            new(
                "cross-source coverage",
                citedSourceIds >= expectedSourceCoverage,
                $"Citations cover {citedSourceIds} source documents; " +
                $"{expectedSourceCoverage} required."),
            new(
                "lexical grounding signal is non-trivial",
                context.EvidenceMatrix.AverageLexicalGroundingScore >= 0.15,
                $"Mean lexical grounding signal: " +
                $"{context.EvidenceMatrix.AverageLexicalGroundingScore:P0}."),
            new(
                "evidence matrix covers every claim",
                context.EvidenceMatrix.Claims.Count == claims.Length,
                $"Evidence matrix contains {context.EvidenceMatrix.Claims.Count} " +
                $"of {claims.Length} claims."),
            new(
                "source risk scan completed",
                File.Exists(context.Artifacts.SourceRiskPath),
                $"Detected {context.SourceRiskReport.Findings.Count} source-risk " +
                $"findings, including {context.SourceRiskReport.HighSeverityCount} high severity."),
            new(
                "report contract is satisfied",
                missingHeadings.Length == 0,
                missingHeadings.Length == 0
                    ? "All required report headings are present."
                    : $"Missing headings: {string.Join(", ", missingHeadings)}."),
            new(
                "report is not empty",
                !string.IsNullOrWhiteSpace(context.Report),
                $"Report length: {context.Report.Length:N0} characters.")
        };

        return Task.FromResult(new EvalReport(checks));
    }

    public static string RenderMarkdown(EvalReport report)
    {
        var builder = new StringBuilder()
            .AppendLine("# Evaluation Report")
            .AppendLine()
            .Append("Overall: **")
            .Append(report.Passed ? "PASS" : "FAIL")
            .AppendLine("**")
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

    private static EvalCheck CheckRequiredArtifacts(RunArtifactPaths artifacts)
    {
        var missing = artifacts.RequiredPaths
            .Where(path => !File.Exists(path))
            .Select(Path.GetFileName)
            .ToArray();

        return new EvalCheck(
            "required artifacts exist",
            missing.Length == 0,
            missing.Length == 0
                ? "All required artifacts exist."
                : $"Missing artifacts: {string.Join(", ", missing)}.");
    }

    private static EvalCheck CheckTraceEvent(
        IReadOnlyList<TraceEvent> events,
        string eventType)
    {
        var found = events.Any(
            traceEvent => string.Equals(
                traceEvent.Type,
                eventType,
                StringComparison.Ordinal));
        return new EvalCheck(
            $"trace contains {eventType}",
            found,
            found
                ? $"Trace contains '{eventType}'."
                : $"Trace does not contain '{eventType}'.");
    }
}
