using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Evaluation;
using DocumentDistiller.Core.Rendering;

namespace DocumentDistiller.Tests;

public sealed class DistillationEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_RejectsUnknownEvidenceId()
    {
        using var workspace = new TestWorkspace();
        var artifacts = CreateArtifacts(workspace.OutputRoot);
        foreach (var path in artifacts.RequiredPaths)
        {
            File.WriteAllText(path, "reserved");
        }

        var documents = new[]
        {
            new SourceDocument("D001", "one.md", "One", "Content", "HASH"),
            new SourceDocument("D002", "two.md", "Two", "Content", "HASH")
        };
        var chunks = new[]
        {
            new DocumentChunk(
                "D001-C001", "D001", 1, 0, 7, "HASH", "Evidence content"),
            new DocumentChunk(
                "D002-C001", "D002", 1, 0, 7, "HASH", "Evaluation content")
        };
        var analysis = new DistillationDraft(
            "Title",
            "Topic",
            "Question?",
            "Summary",
            [
                new Pillar(
                    "P01",
                    "One",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C01",
                            "Evidence content supports the first claim.",
                            ClaimStances.SingleSource,
                            0.7,
                            ["D001-C001"])
                    ]),
                new Pillar(
                    "P02",
                    "Two",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C02",
                            "Unknown evidence supports the second claim.",
                            ClaimStances.SingleSource,
                            0.7,
                            ["D999-C999"])
                    ])
            ],
            ["Theme"],
            ["Tension"],
            ["Gap"],
            "Conclusion");
        var evidenceMatrix = new EvidenceMatrixBuilder().Build(
            analysis,
            documents,
            chunks);
        var sourceRiskReport = new SourceRiskReport([]);
        var report = MarkdownReportRenderer.Render(
            analysis,
            documents,
            evidenceMatrix,
            sourceRiskReport);
        var events = new[]
        {
            Event("run.started"),
            Event("source_risk.scanned"),
            Event("critic.completed"),
            Event("revision.completed"),
            Event("evidence_matrix.completed"),
            Event("run.completed")
        };

        var result = await new DistillationEvaluator().EvaluateAsync(
            new EvalContext(
                artifacts,
                documents,
                chunks,
                analysis,
                evidenceMatrix,
                sourceRiskReport,
                report,
                events));

        Assert.False(result.Passed);
        var citationCheck = Assert.Single(
            result.Checks,
            check => check.Name == "all citations resolve");
        Assert.Contains("D999-C999", citationCheck.Details);
    }

    private static TraceEvent Event(string type) =>
        new(
            DateTimeOffset.UnixEpoch,
            type,
            "run",
            new Dictionary<string, object?>());

    private static RunArtifactPaths CreateArtifacts(string root) =>
        new(
            root,
            Path.Combine(root, "input_manifest.md"),
            Path.Combine(root, "run_manifest.json"),
            Path.Combine(root, "source_risk.json"),
            Path.Combine(root, "evidence.json"),
            Path.Combine(root, "evidence_matrix.json"),
            Path.Combine(root, "model_usage.json"),
            Path.Combine(root, "draft.json"),
            Path.Combine(root, "critique.json"),
            Path.Combine(root, "analysis.json"),
            Path.Combine(root, "report.md"),
            Path.Combine(root, "index.html"),
            Path.Combine(root, "trace.json"),
            Path.Combine(root, "run_summary.md"),
            Path.Combine(root, "eval_report.md"));
}
