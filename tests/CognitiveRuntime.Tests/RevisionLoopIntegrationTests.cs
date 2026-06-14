using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class RevisionLoopIntegrationTests
{
    public static TheoryData<string, string[]> MockModeCases =>
        new()
        {
            {
                "frame",
                ["## Problem", "## Objective", "## Constraints", "## Unknowns", "## Next Actions"]
            },
            {
                "challenge",
                ["## Target Claim", "## Assumptions", "## Failure Modes", "## Counterarguments", "## Tests"]
            },
            {
                "synthesize",
                ["## Shared Ground", "## Tensions", "## Synthesis", "## Tradeoffs", "## Recommendation"]
            }
        };

    [Theory]
    [MemberData(nameof(MockModeCases))]
    public async Task RunAsync_MockModesProducePassingAuthoritativeRevisions(
        string modeName,
        string[] requiredHeadings)
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode(modeName, requiredHeadings);
        var orchestrator = CreateOrchestrator(workspace, TimeProvider.System);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                modeName,
                $"Exercise the deterministic {modeName} revision.",
                "mock",
                workspace.OutputRoot));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        var resultMarkdown = await File.ReadAllTextAsync(result.ResultPath);
        Assert.Contains("## Authoritative Revision", resultMarkdown);
        Assert.All(
            requiredHeadings,
            heading => Assert.Contains(heading, resultMarkdown));
    }

    [Fact]
    public async Task RunAsync_ProvidesTypedPriorResultsToEachPhase()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var modelClient = new CapturingModelClient();
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            modelClient);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Capture the typed phase context.",
                modelClient.ProviderName,
                workspace.OutputRoot));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        Assert.Collection(
            modelClient.Requests,
            main => Assert.Empty(main.PriorPhaseResults),
            critic =>
            {
                var prior = Assert.Single(critic.PriorPhaseResults);
                Assert.Equal("main", prior.PhaseName);
                Assert.Equal(PhaseKind.Main, prior.PhaseKind);
            },
            revision =>
            {
                Assert.Collection(
                    revision.PriorPhaseResults,
                    main =>
                    {
                        Assert.Equal("main", main.PhaseName);
                        Assert.Equal(PhaseKind.Main, main.PhaseKind);
                    },
                    critic =>
                    {
                        Assert.Equal("critic", critic.PhaseName);
                        Assert.Equal(PhaseKind.Critic, critic.PhaseKind);
                    });

                var list = Assert.IsAssignableFrom<IList<PhaseResult>>(
                    revision.PriorPhaseResults);
                Assert.True(list.IsReadOnly);
                Assert.Throws<NotSupportedException>(
                    () => list.Add(
                        new PhaseResult(
                            "extra",
                            PhaseKind.Main,
                            "content",
                            "test",
                            null)));
            });
    }

    [Fact]
    public async Task RunAsync_EmptyRevisionRecordsTerminalFailureAndArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var modelClient = new ConfigurableRevisionModelClient(
            _ => Task.FromResult(
                new ModelResponse(string.Empty, "revision-test", "empty")));
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            modelClient);

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Produce an empty revision.",
                    modelClient.ProviderName,
                    workspace.OutputRoot)));

        Assert.IsType<ModelProviderException>(exception.InnerException);
        AssertRequiredArtifactsExist(exception.OutputDirectory);
        var eventTypes = await ReadTraceEventTypesAsync(exception.OutputDirectory);
        Assert.Contains("revision.started", eventTypes);
        Assert.DoesNotContain("revision.completed", eventTypes);
        await AssertTerminalFailureAsync(exception.OutputDirectory);
    }

    [Fact]
    public async Task RunAsync_RevisionProviderFailureRecordsTerminalFailureAndArtifacts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var failure = new ModelProviderException("Synthetic revision failure.");
        var modelClient = new ConfigurableRevisionModelClient(
            _ => Task.FromException<ModelResponse>(failure));
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            modelClient);

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Fail during the revision phase.",
                    modelClient.ProviderName,
                    workspace.OutputRoot)));

        Assert.Same(failure, exception.InnerException);
        AssertRequiredArtifactsExist(exception.OutputDirectory);
        var eventTypes = await ReadTraceEventTypesAsync(exception.OutputDirectory);
        Assert.Contains("revision.started", eventTypes);
        Assert.DoesNotContain("revision.completed", eventTypes);
        await AssertTerminalFailureAsync(exception.OutputDirectory);
    }

    [Fact]
    public async Task RunAsync_MalformedRevisionFailsEvalWithoutUsingCriticHeadings()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var modelClient = new ConfigurableRevisionModelClient(
            _ => Task.FromResult(
                new ModelResponse(
                    "This revision deliberately violates the heading contract.",
                    "revision-test",
                    "malformed")));
        var orchestrator = CreateOrchestrator(
            workspace,
            TimeProvider.System,
            modelClient);

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Return a malformed authoritative revision.",
                modelClient.ProviderName,
                workspace.OutputRoot));

        Assert.Equal(RunOutcome.EvalFailed, result.Outcome);
        AssertRequiredArtifactsExist(result.OutputDirectory);
        var evalMarkdown = await File.ReadAllTextAsync(result.EvalReportPath);
        Assert.Contains("Overall: **FAIL**", evalMarkdown);
        Assert.Contains("Missing headings", evalMarkdown);
        var eventTypes = await ReadTraceEventTypesAsync(result.OutputDirectory);
        Assert.Contains("revision.completed", eventTypes);
        Assert.DoesNotContain("run.failed", eventTypes);
        Assert.Equal("run.finalized", eventTypes[^1]);
        using var manifest = System.Text.Json.JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(result.OutputDirectory, "run.json")));
        Assert.Equal(
            "evalFailed",
            manifest.RootElement.GetProperty("outcome").GetString());
        Assert.False(
            manifest.RootElement
                .GetProperty("evaluation")
                .GetProperty("passed")
                .GetBoolean());
    }

    private sealed class CapturingModelClient : IModelClient
    {
        private readonly MockModelClient _inner = new();
        private readonly List<ModelRequest> _requests = [];

        public string ProviderName => "capturing";

        public IReadOnlyList<ModelRequest> Requests => _requests;

        public async Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            _requests.Add(request);
            var response = await _inner.CompleteAsync(request, cancellationToken);
            return response with { Provider = ProviderName };
        }
    }

    private sealed class ConfigurableRevisionModelClient : IModelClient
    {
        private readonly MockModelClient _inner = new();
        private readonly Func<ModelRequest, Task<ModelResponse>> _revision;

        public ConfigurableRevisionModelClient(
            Func<ModelRequest, Task<ModelResponse>> revision)
        {
            _revision = revision;
        }

        public string ProviderName => "revision-test";

        public async Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.PhaseKind == PhaseKind.Revision)
            {
                return await _revision(request);
            }

            var response = await _inner.CompleteAsync(request, cancellationToken);
            return response with { Provider = ProviderName };
        }
    }
}
