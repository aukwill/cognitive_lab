using System.Text.Json;
using DocumentDistiller.Core.Artifacts;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Evaluation;
using DocumentDistiller.Core.Ingestion;
using DocumentDistiller.Core.Models;
using DocumentDistiller.Core.Runtime;
using DocumentDistiller.Core.Safety;
using DocumentDistiller.Core.Tracing;

namespace DocumentDistiller.Tests;

public sealed class OrchestratorIntegrationTests
{
    [Fact]
    public async Task RunAsync_MockRunWritesPassingTraceableArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.AddDocument(
            "runtime.md",
            "# Runtime\n\nEvidence and orchestration make model systems inspectable.");
        workspace.AddDocument(
            "evals.md",
            "# Evals\n\nEvaluation verifies evidence and catches broken citations.");
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(timeProvider);

        var result = await orchestrator.RunAsync(
            new DistillationRunRequest(
                workspace.InputRoot,
                workspace.OutputRoot,
                workspace.PromptsRoot,
                "mock",
                ChunkSizeCharacters: 200,
                ChunkOverlapCharacters: 20));

        Assert.Equal(DistillationRunOutcome.Success, result.Outcome);
        Assert.All(
            new[]
            {
                "input_manifest.md",
                "run_manifest.json",
                "source_risk.json",
                "evidence.json",
                "evidence_matrix.json",
                "model_usage.json",
                "draft.json",
                "critique.json",
                "analysis.json",
                "report.md",
                "index.html",
                "trace.json",
                "run_summary.md",
                "eval_report.md"
            },
            name => Assert.True(
                File.Exists(Path.Combine(result.OutputDirectory, name)),
                $"Missing artifact: {name}"));

        var report = await File.ReadAllTextAsync(result.ReportPath);
        Assert.Contains("## Central Pillars", report);
        Assert.Contains("#### Claim", report);
        Assert.Contains("## Evidence Quality", report);
        Assert.Contains("[D001-C001]", report);
        Assert.Contains("[D002-C001]", report);

        var html = await File.ReadAllTextAsync(result.HtmlReportPath);
        Assert.Contains("Evidence-first distillation", html);
        Assert.Contains("D001-C001", html);
        Assert.Contains("evidence_matrix.json", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);

        var eval = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("Overall: **PASS**", eval);

        using var trace = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.TracePath));
        var events = trace.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .Select(element => element.GetProperty("type").GetString())
            .ToArray();

        Assert.True(Array.IndexOf(events, "analysis.completed") <
                    Array.IndexOf(events, "critic.started"));
        Assert.True(Array.IndexOf(events, "critic.completed") <
                    Array.IndexOf(events, "revision.started"));
        Assert.True(Array.IndexOf(events, "revision.completed") <
                    Array.IndexOf(events, "eval.started"));
        Assert.Contains("source_risk.scanned", events);
        Assert.Contains("analysis.validated", events);
        Assert.Contains("revision.validated", events);
        Assert.Contains("evidence_matrix.completed", events);
        Assert.Equal("run.finalized", events[^1]);

        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(result.OutputDirectory, "run_manifest.json")));
        Assert.Equal(
            "deterministic-keyword-v3",
            manifest.RootElement.GetProperty("model").GetString());
        Assert.Equal(64, manifest.RootElement.GetProperty("corpusSha256").GetString()!.Length);
    }

    private static DistillationOrchestrator CreateOrchestrator(
        TimeProvider timeProvider) =>
        new(
            new DocumentIngestor(),
            new FilePromptLoader(),
            new DistillationModelClientFactory(
                [new MockDistillationModelClient()]),
            new RunArtifactWriter(timeProvider),
            new JsonTraceSessionFactory(timeProvider),
            new DistillationEvaluator(),
            new SourceRiskScanner(),
            new EvidenceMatrixBuilder(),
            new DistillationContractValidator(),
            timeProvider);
}
