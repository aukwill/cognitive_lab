using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

public sealed class ExecutionNodeStateTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_RecordsRuntimeOwnedNodeMetadata()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var timeProvider = new FixedTimeProvider(FixedNow);
        var artifactWriter = new ArtifactWriter(timeProvider);
        var plan = new CriticRevisionPattern().CreatePlan(
            new PatternPlanRequest("frame", null, null));
        var snapshots = new List<IReadOnlyList<ExecutionNodeState>>();

        var result = await ExecuteAsync(
            workspace,
            timeProvider,
            artifactWriter,
            plan,
            new MockModelClient(),
            states => snapshots.Add(states.ToArray()));

        Assert.Equal(3, result.NodeStates.Count);
        Assert.All(
            result.NodeStates,
            node =>
            {
                Assert.Equal(ExecutionNodeStatus.Completed, node.Status);
                Assert.Equal("frame", node.ModeName);
                Assert.Equal("mock", node.Provider);
                Assert.Equal("deterministic-template-v2", node.Model);
                Assert.Equal(FixedNow, node.StartedAt);
                Assert.Equal(FixedNow, node.EndedAt);
                Assert.True(node.OutputLength > 0);
            });
        Assert.Equal(
            ["main", "critic", "revision"],
            result.NodeStates.Select(node => node.PhaseName).ToArray());
        Assert.All(
            snapshots[0],
            node => Assert.Equal(ExecutionNodeStatus.Pending, node.Status));
        Assert.Contains(
            snapshots,
            snapshot =>
                snapshot[0].Status == ExecutionNodeStatus.Running &&
                snapshot[1].Status == ExecutionNodeStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_FailureMarksCurrentNodeFailedAndFutureNodesCancelled()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var timeProvider = new FixedTimeProvider(FixedNow);
        var artifactWriter = new ArtifactWriter(timeProvider);
        var plan = new CriticRevisionPattern().CreatePlan(
            new PatternPlanRequest("frame", null, null));
        IReadOnlyList<ExecutionNodeState>? latest = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteAsync(
                workspace,
                timeProvider,
                artifactWriter,
                plan,
                new FailingSecondCallModelClient(),
                states => latest = states.ToArray()));

        Assert.NotNull(latest);
        Assert.Equal(
            [
                ExecutionNodeStatus.Completed,
                ExecutionNodeStatus.Failed,
                ExecutionNodeStatus.Cancelled
            ],
            latest.Select(node => node.Status).ToArray());
        Assert.Equal(FixedNow, latest[1].StartedAt);
        Assert.Equal(FixedNow, latest[1].EndedAt);
        Assert.Null(latest[1].OutputLength);
        Assert.Null(latest[2].StartedAt);
        Assert.Equal(FixedNow, latest[2].EndedAt);
    }

    [Fact]
    public void RunState_PipelineNodesUseStageIdsForGrouping()
    {
        var plan = new LinearPipelinePattern().CreatePlan(
            new PatternPlanRequest(
                "pipeline",
                null,
                ["frame", "challenge"]));
        var state = RunStateUpdates.Create(
            "run-001",
            plan,
            new RunArtifactPaths(
                "outputs/run-001",
                "outputs/run-001/input.md",
                "outputs/run-001/result.md",
                "outputs/run-001/trace.json",
                "outputs/run-001/run_summary.md",
                "outputs/run-001/eval_report.md",
                "outputs/run-001/pattern.md"));

        Assert.Equal(6, state.ExecutionNodes.Length);
        Assert.Equal(
            ["stage-01", "stage-02"],
            state.ExecutionNodes
                .Select(node => node.StageId!)
                .Distinct()
                .ToArray());
        Assert.All(
            state.ExecutionNodes.Take(3),
            node => Assert.Equal("stage-01", node.StageId));
        Assert.All(
            state.ExecutionNodes.Skip(3),
            node => Assert.Equal("stage-02", node.StageId));
    }

    private static async Task<PatternExecutionResult> ExecuteAsync(
        TestWorkspace workspace,
        TimeProvider timeProvider,
        ArtifactWriter artifactWriter,
        PatternExecutionPlan plan,
        IModelClient modelClient,
        Action<IReadOnlyList<ExecutionNodeState>> nodeStatesChanged)
    {
        var artifacts = await artifactWriter.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "run-001");
        var trace = await new JsonTraceSessionFactory(timeProvider).CreateAsync(
            "run-001",
            artifacts.TracePath);
        var executor = new PatternExecutor(
            new FileModeLoader(workspace.ModesRoot),
            new PhaseRunner(timeProvider),
            artifactWriter,
            timeProvider);

        return await executor.ExecuteAsync(
            "run-001",
            "Inspect execution state.",
            plan,
            modelClient,
            artifacts,
            trace,
            nodeStatesChanged);
    }

    private sealed class FailingSecondCallModelClient : IModelClient
    {
        private int _callCount;

        public string ProviderName => "state-test";

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            _callCount++;
            if (_callCount == 2)
            {
                throw new InvalidOperationException("Synthetic critic failure.");
            }

            return Task.FromResult(
                new ModelResponse(
                    "Completed node output.",
                    ProviderName,
                    "state-test-model"));
        }
    }
}
