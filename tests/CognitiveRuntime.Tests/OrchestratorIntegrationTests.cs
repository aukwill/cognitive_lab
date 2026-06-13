using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

public sealed class OrchestratorIntegrationTests
{
    [Fact]
    public async Task RunAsync_MockFrameRunWritesValidPassingArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(workspace, timeProvider);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Build a traceable local-first cognitive runtime.",
                "mock",
                workspace.OutputRoot));

        Assert.True(result.EvalPassed);
        Assert.All(
            new[]
            {
                Path.Combine(result.OutputDirectory, "input.md"),
                result.ResultPath,
                result.TracePath,
                Path.Combine(result.OutputDirectory, "run_summary.md"),
                result.EvalReportPath
            },
            path => Assert.True(File.Exists(path), $"Missing artifact: {path}"));

        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Problem", resultMarkdown);
        Assert.Contains("## Critic Review", resultMarkdown);

        var evalMarkdown = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("Overall: **PASS**", evalMarkdown);

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var eventTypes = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .Select(element => element.GetProperty("type").GetString())
            .ToArray();

        Assert.Contains("run.started", eventTypes);
        Assert.Contains("mode.loaded", eventTypes);
        Assert.Contains("phase.started", eventTypes);
        Assert.Contains("model.called", eventTypes);
        Assert.Contains("model.completed", eventTypes);
        Assert.Contains("critic.started", eventTypes);
        Assert.Contains("critic.completed", eventTypes);
        Assert.Contains("artifact.written", eventTypes);
        Assert.Contains("eval.started", eventTypes);
        Assert.Contains("eval.completed", eventTypes);
        Assert.Contains("run.completed", eventTypes);
        Assert.Equal("run.finalized", eventTypes[^1]);
    }

    [Fact]
    public async Task RunAsync_ModelFailureLeavesRequiredFailureArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            new FailingModelClient());

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Input that reaches a failing provider.",
                    "failing",
                    workspace.OutputRoot)));

        var requiredFiles = new[]
        {
            "input.md",
            "result.md",
            "trace.json",
            "run_summary.md",
            "eval_report.md"
        };

        Assert.All(
            requiredFiles,
            file => Assert.True(
                File.Exists(Path.Combine(exception.OutputDirectory, file)),
                $"Missing failure artifact: {file}"));

        var trace = await File.ReadAllTextAsync(
            Path.Combine(exception.OutputDirectory, "trace.json"));
        Assert.Contains("\"type\": \"run.failed\"", trace);

        var eval = await File.ReadAllTextAsync(
            Path.Combine(exception.OutputDirectory, "eval_report.md"));
        Assert.Contains("Overall: **FAIL**", eval);
    }

    private static Orchestrator CreateOrchestrator(
        TestWorkspace workspace,
        TimeProvider timeProvider,
        IModelClient? modelClient = null)
    {
        IModelClient[] modelClients = [modelClient ?? new MockModelClient()];

        return new Orchestrator(
            new FileModeLoader(workspace.ModesRoot),
            new ModelClientFactory(modelClients),
            new PhaseRunner(),
            new ArtifactWriter(timeProvider),
            new JsonTraceSessionFactory(timeProvider),
            new EvalRunner(new OutputContractValidator()));
    }

    private sealed class FailingModelClient : IModelClient
    {
        public string ProviderName => "failing";

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default) =>
            throw new ModelProviderException("Synthetic provider failure.");
    }
}
