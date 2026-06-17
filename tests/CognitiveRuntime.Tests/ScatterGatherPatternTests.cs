using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Models;
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
    public async Task RunAsync_RunsIndependentBranchesConcurrently()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        // The probe blocks each branch call until both are in flight, so the run
        // can only complete if the two branches execute concurrently. If they
        // ran sequentially the first call would wait forever (its timeout would
        // surface as a run failure rather than a hang).
        var probe = new ConcurrencyProbeClient(expectedConcurrency: 2);
        var orchestrator = CreateOrchestrator(
            workspace,
            FixedTime(),
            probe,
            maxBranchConcurrency: 2);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Branches should overlap.",
                "concurrency-probe",
                workspace.OutputRoot,
                Pattern: "scatter-gather",
                ScatterModes: ["frame", "frame"]));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        Assert.Equal(2, probe.MaxObservedConcurrency);
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

    // Releases each call only once `expectedConcurrency` calls are simultaneously
    // in flight, so it both forces and measures branch overlap. The barrier wait
    // has a timeout so a regression to sequential execution fails the test
    // instead of hanging it.
    private sealed class ConcurrencyProbeClient(int expectedConcurrency) : IModelClient
    {
        private readonly MockModelClient _inner = new();
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _sync = new();
        private int _arrived;
        private int _inFlight;

        public string ProviderName => "concurrency-probe";

        public int MaxObservedConcurrency { get; private set; }

        public async Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _inFlight++;
                MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, _inFlight);
                if (++_arrived >= expectedConcurrency)
                {
                    _allArrived.TrySetResult();
                }
            }

            try
            {
                await _allArrived.Task.WaitAsync(
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                var response = await _inner.CompleteAsync(request, cancellationToken);
                return response with { Provider = ProviderName };
            }
            finally
            {
                lock (_sync)
                {
                    _inFlight--;
                }
            }
        }
    }
}
