using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class PipelineOrchestratorTests
{
    [Fact]
    public async Task RunAsync_LinearPipelinePatternWritesStageArtifactsAndPassingEval()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode("frame");
        workspace.CreateMode(
            "challenge",
            requiredHeadings:
            [
                "## Target Claim",
                "## Assumptions",
                "## Failure Modes",
                "## Counterarguments",
                "## Tests"
            ]);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(workspace, timeProvider);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "pipeline",
                "Build a traceable local-first cognitive runtime.",
                "mock",
                workspace.OutputRoot,
                Pattern: "linear-pipeline",
                PipelineStages: ["frame", "challenge"]));

        Assert.Equal(RunOutcome.Success, result.Outcome);

        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Authoritative Revision", resultMarkdown);
        Assert.Contains("## Target Claim", resultMarkdown);

        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "01-frame", "input.md")));
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "01-frame", "result.md")));
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "02-challenge", "input.md")));
        Assert.True(File.Exists(
            Path.Combine(result.OutputDirectory, "stages", "02-challenge", "result.md")));

        var runSummaryMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "run_summary.md"));
        Assert.Contains("Pattern: `linear-pipeline`", runSummaryMarkdown);
        Assert.Contains("frame -> challenge", runSummaryMarkdown);

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var traceEvents = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();
        var eventTypes = traceEvents
            .Select(element => element.GetProperty("type").GetString() ?? string.Empty)
            .ToArray();

        var runStartedEvent = traceEvents.Single(
            traceEvent => traceEvent.GetProperty("type").GetString() == "run.started");
        Assert.Equal(
            "linear-pipeline",
            runStartedEvent.GetProperty("data").GetProperty("pattern").GetString());

        Assert.Contains("run.completed", eventTypes);
        Assert.Contains("eval.started", eventTypes);
        Assert.Contains("eval.completed", eventTypes);
        Assert.Contains("run.finalized", eventTypes);
        Assert.Equal("run.finalized", eventTypes[^1]);
    }

    [Fact]
    public async Task RunAsync_SinglePassPatternRunsOnlyMainPhase()
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
                workspace.OutputRoot,
                Pattern: "single-pass"));

        Assert.Equal(RunOutcome.EvalFailed, result.Outcome);
        AssertRequiredArtifactsExist(result.OutputDirectory);

        var runSummaryMarkdown = await File.ReadAllTextAsync(
            Path.Combine(result.OutputDirectory, "run_summary.md"));
        Assert.Contains("Pattern: `single-pass`", runSummaryMarkdown);

        var eventTypes = await ReadTraceEventTypesAsync(result.OutputDirectory);
        Assert.DoesNotContain("critic.started", eventTypes);
        Assert.DoesNotContain("revision.started", eventTypes);
        Assert.Contains("run.finalized", eventTypes);
    }
}
