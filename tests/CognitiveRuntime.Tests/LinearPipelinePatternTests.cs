using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Modes;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Runtime.Orchestration;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

public sealed class LinearPipelinePatternTests
{
    [Fact]
    public async Task RunAsync_RunsStagesInConfiguredOrder()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode("frame");
        workspace.CreateMode("challenge");
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));

        var modeLoader = new FileModeLoader(workspace.ModesRoot);
        var modelClient = new MockModelClient();
        var phaseRunner = new PhaseRunner();
        var artifactWriter = new ArtifactWriter(timeProvider);
        var rootArtifacts = await artifactWriter.PrepareRunAsync(
            workspace.OutputRoot, "linear-pipeline", "run-001");
        var trace = await new JsonTraceSessionFactory(timeProvider)
            .CreateAsync("run-001", rootArtifacts.TracePath);

        var pattern = new LinearPipelinePattern(["frame", "challenge"]);

        var stages = await pattern.RunAsync(
            "run-001",
            "Build a traceable local-first cognitive runtime.",
            modeLoader,
            modelClient,
            phaseRunner,
            artifactWriter,
            rootArtifacts,
            trace);

        Assert.Equal(2, stages.Count);

        Assert.Equal(1, stages[0].StageIndex);
        Assert.Equal("frame", stages[0].ModeName);
        Assert.Equal(2, stages[1].StageIndex);
        Assert.Equal("challenge", stages[1].ModeName);

        Assert.All(
            stages,
            stage =>
            {
                Assert.Equal(3, stage.PhaseResults.Count);
                Assert.Contains(stage.PhaseResults, result => result.PhaseKind == PhaseKind.Main);
                Assert.Contains(stage.PhaseResults, result => result.PhaseKind == PhaseKind.Critic);
                Assert.Contains(stage.PhaseResults, result => result.PhaseKind == PhaseKind.Revision);
            });

        Assert.True(File.Exists(
            Path.Combine(rootArtifacts.RunDirectory, "stages", "01-frame", "input.md")));
        Assert.True(File.Exists(
            Path.Combine(rootArtifacts.RunDirectory, "stages", "01-frame", "result.md")));
        Assert.True(File.Exists(
            Path.Combine(rootArtifacts.RunDirectory, "stages", "02-challenge", "input.md")));
        Assert.True(File.Exists(
            Path.Combine(rootArtifacts.RunDirectory, "stages", "02-challenge", "result.md")));
    }

    [Fact]
    public async Task RunAsync_PassesPriorStageRevisionAsNextStageInput()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode("frame");
        workspace.CreateMode("challenge");
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));

        var modeLoader = new FileModeLoader(workspace.ModesRoot);
        var modelClient = new CapturingModelClient(new MockModelClient());
        var phaseRunner = new PhaseRunner();
        var artifactWriter = new ArtifactWriter(timeProvider);
        var rootArtifacts = await artifactWriter.PrepareRunAsync(
            workspace.OutputRoot, "linear-pipeline", "run-002");
        var trace = await new JsonTraceSessionFactory(timeProvider)
            .CreateAsync("run-002", rootArtifacts.TracePath);

        var pattern = new LinearPipelinePattern(["frame", "challenge"]);
        const string initialInput = "Build a traceable local-first cognitive runtime.";

        var stages = await pattern.RunAsync(
            "run-002",
            initialInput,
            modeLoader,
            modelClient,
            phaseRunner,
            artifactWriter,
            rootArtifacts,
            trace);

        var stage1MainRequest = modelClient.Requests.Single(
            request => request.ModeName == "frame" && request.PhaseKind == PhaseKind.Main);
        var stage2MainRequest = modelClient.Requests.Single(
            request => request.ModeName == "challenge" && request.PhaseKind == PhaseKind.Main);

        Assert.Equal(initialInput, stage1MainRequest.Input);
        Assert.Equal(stages[0].RevisionContent, stage2MainRequest.Input);
    }

    [Fact]
    public void Constructor_ThrowsForEmptyOrBlankStageList()
    {
        Assert.Throws<ArgumentException>(() => new LinearPipelinePattern([]));
        Assert.Throws<ArgumentException>(() => new LinearPipelinePattern(["frame", ""]));
    }

    private sealed class CapturingModelClient : IModelClient
    {
        private readonly IModelClient _inner;
        private readonly List<ModelRequest> _requests = [];

        public CapturingModelClient(IModelClient inner)
        {
            _inner = inner;
        }

        public string ProviderName => _inner.ProviderName;

        public IReadOnlyList<ModelRequest> Requests => _requests;

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            _requests.Add(request);
            return _inner.CompleteAsync(request, cancellationToken);
        }
    }
}
