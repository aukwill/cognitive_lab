using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class ScatterGatherPatternTests
{
    private static FixedTimeProvider FixedTime() =>
        new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task RunAsync_RunsEveryBranchAndAuthoritativeGather()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(workspace, FixedTime());

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Bound a scatter-gather run over independent branches.",
                "mock",
                workspace.OutputRoot,
                Pattern: "scatter-gather",
                ScatterModes: ["frame", "frame"]));

        Assert.Equal(RunOutcome.Success, result.Outcome);

        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(result.OutputDirectory, "run.json")));
        var root = manifest.RootElement;
        Assert.Equal("scatter-gather", root.GetProperty("patternName").GetString());

        var plan = root.GetProperty("plan");
        Assert.Equal(
            ["branch-01", "branch-02", "gather"],
            plan.GetProperty("nodes")
                .EnumerateArray()
                .Select(node => node.GetProperty("id").GetString() ?? string.Empty)
                .ToArray());
        Assert.Equal("gather", plan.GetProperty("authoritativeNodeId").GetString());

        // The gather depends on and reads every branch, in declared order.
        var gather = plan.GetProperty("nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("id").GetString() == "gather");
        Assert.Equal(
            ["branch-01", "branch-02"],
            gather.GetProperty("contextInputs")
                .EnumerateArray()
                .Select(id => id.GetString() ?? string.Empty)
                .ToArray());

        // Every declared node ran exactly once and completed.
        Assert.All(
            root.GetProperty("executionNodes").EnumerateArray(),
            node => Assert.Equal(
                "completed",
                node.GetProperty("status").GetString()));
        Assert.True(
            root.GetProperty("evaluation").GetProperty("passed").GetBoolean());

        var evalMarkdown = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("Overall: **PASS**", evalMarkdown);
        Assert.Contains("declared plan execution", evalMarkdown);
        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Problem", resultMarkdown);
    }

    [Fact]
    public async Task RunAsync_ScatterGatherSerializationIsDeterministic()
    {
        using var firstWorkspace = new TestWorkspace();
        using var secondWorkspace = new TestWorkspace();
        firstWorkspace.CreateMode();
        secondWorkspace.CreateMode();
        var timestamp = new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
        var request = new RunRequest(
            "frame",
            "Deterministic scatter-gather run.",
            "mock",
            firstWorkspace.OutputRoot,
            Pattern: "scatter-gather",
            ScatterModes: ["frame", "frame"]);

        var first = CreateOrchestrator(
            firstWorkspace,
            new FixedTimeProvider(timestamp),
            runIdGenerator: new FixedRunIdGenerator("run-scatter"));
        var second = CreateOrchestrator(
            secondWorkspace,
            new FixedTimeProvider(timestamp),
            runIdGenerator: new FixedRunIdGenerator("run-scatter"));

        var firstResult = await first.RunAsync(request);
        var secondResult = await second.RunAsync(
            request with { OutputRoot = secondWorkspace.OutputRoot });

        Assert.Equal(
            await File.ReadAllTextAsync(
                Path.Combine(firstResult.OutputDirectory, "run.json")),
            await File.ReadAllTextAsync(
                Path.Combine(secondResult.OutputDirectory, "run.json")));
    }
}
