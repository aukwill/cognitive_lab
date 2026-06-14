using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime.Orchestration;

namespace CognitiveRuntime.Tests;

public sealed class PatternExecutionPlanValidatorTests
{
    private readonly PatternExecutionPlanValidator _validator = new();

    [Fact]
    public void Validate_AcceptsEveryBuiltInPattern()
    {
        var requests = new[]
        {
            (Pattern: (IOrchestrationPattern)new SinglePassPattern(),
                Request: new PatternPlanRequest("frame", null, null)),
            (Pattern: (IOrchestrationPattern)new CriticRevisionPattern(),
                Request: new PatternPlanRequest("frame", null, null)),
            (Pattern: (IOrchestrationPattern)new LinearPipelinePattern(),
                Request: new PatternPlanRequest(
                    "pipeline",
                    null,
                    ["frame", "challenge"]))
        };

        foreach (var item in requests)
        {
            _validator.Validate(item.Pattern.CreatePlan(item.Request));
        }
    }

    [Fact]
    public void Validate_RejectsDuplicateNodeIds()
    {
        var plan = CreateCriticRevisionPlan();
        var mutated = plan with
        {
            Nodes =
            [
                plan.Nodes[0],
                plan.Nodes[1] with { Id = plan.Nodes[0].Id },
                plan.Nodes[2]
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(mutated));

        Assert.Contains("duplicate node ID", exception.Message);
    }

    [Fact]
    public void Validate_RejectsMissingDependencies()
    {
        var plan = CreateCriticRevisionPlan();
        var mutated = plan with
        {
            Nodes =
            [
                plan.Nodes[0],
                plan.Nodes[1] with { DependencyNodeIds = ["missing"] },
                plan.Nodes[2]
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(mutated));

        Assert.Contains("undeclared node 'missing'", exception.Message);
    }

    [Fact]
    public void Validate_RejectsInvalidAuthority()
    {
        var plan = CreateCriticRevisionPlan() with
        {
            AuthoritativeNodeId = "missing"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(plan));

        Assert.Contains("authoritative node 'missing' is not declared", exception.Message);
    }

    [Fact]
    public void Validate_RejectsFutureContextEdges()
    {
        var plan = CreateCriticRevisionPlan();
        var mutated = plan with
        {
            Nodes =
            [
                plan.Nodes[0],
                plan.Nodes[1] with
                {
                    DependencyNodeIds = ["revision"],
                    ContextNodeIds = ["revision"]
                },
                plan.Nodes[2]
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(mutated));

        Assert.Contains("references future node 'revision'", exception.Message);
    }

    [Fact]
    public void Validate_RejectsUnknownPhaseKinds()
    {
        var plan = CreateCriticRevisionPlan();
        var mutated = plan with
        {
            Nodes =
            [
                plan.Nodes[0] with { PhaseKind = (PhaseKind)999 },
                plan.Nodes[1],
                plan.Nodes[2]
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(mutated));

        Assert.Contains("unknown phase kind", exception.Message);
    }

    [Fact]
    public async Task Orchestrator_RejectsInvalidPlanBeforeFirstModelCall()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var modelClient = new CountingModelClient();
        var invalidPattern = new InvalidPattern();
        var orchestrator = RuntimeTestHarness.CreateOrchestrator(
            workspace,
            TimeProvider.System,
            modelClient,
            patternFactory: new OrchestrationPatternFactory([invalidPattern]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Validate before execution.",
                    modelClient.ProviderName,
                    workspace.OutputRoot,
                    Pattern: invalidPattern.Name)));

        Assert.Equal(0, modelClient.CallCount);
    }

    private static PatternExecutionPlan CreateCriticRevisionPlan() =>
        new CriticRevisionPattern().CreatePlan(
            new PatternPlanRequest("frame", null, null));

    private sealed class InvalidPattern : IOrchestrationPattern
    {
        public string Name => "invalid";

        public PatternExecutionPlan CreatePlan(PatternPlanRequest request)
        {
            var plan = new SinglePassPattern().CreatePlan(request);
            return plan with { PatternName = Name, AuthoritativeNodeId = "missing" };
        }
    }

    private sealed class CountingModelClient : IModelClient
    {
        public string ProviderName => "counting";

        public int CallCount { get; private set; }

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(
                new ModelResponse("unexpected", ProviderName, "test"));
        }
    }
}
