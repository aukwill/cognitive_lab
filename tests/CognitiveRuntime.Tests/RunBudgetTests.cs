using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class RunBudgetTests
{
    private static FixedTimeProvider FixedTime() =>
        new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task RunAsync_MaxInputCharacters_FailsWithBudgetBreach()
    {
        var input = new string('x', 200);
        var (kind, limit, observed) = await RunAndReadBreachAsync(
            RunBudget.Default with { MaxInputCharacters = 50 },
            input: input);

        Assert.Equal("inputCharacters", kind);
        Assert.Equal(50, limit);
        Assert.Equal(input.Length, observed);
    }

    [Fact]
    public async Task RunAsync_MaxModelCalls_FailsWithBudgetBreach()
    {
        // critic-revision makes three model calls; a budget of one trips on the
        // second node before its call.
        var (kind, limit, observed) = await RunAndReadBreachAsync(
            RunBudget.Default with { MaxModelCalls = 1 });

        Assert.Equal("modelCalls", kind);
        Assert.Equal(1, limit);
        Assert.Equal(2, observed);
    }

    [Fact]
    public async Task RunAsync_MaxPhaseOutputCharacters_FailsWithBudgetBreach()
    {
        var (kind, limit, observed) = await RunAndReadBreachAsync(
            RunBudget.Default with { MaxPhaseOutputCharacters = 5 });

        Assert.Equal("phaseOutputCharacters", kind);
        Assert.Equal(5, limit);
        Assert.True(observed > 5);
    }

    [Fact]
    public async Task RunAsync_MaxRunDuration_FailsWithBudgetBreach()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var timeProvider = FixedTime();
        var orchestrator = CreateOrchestrator(
            workspace,
            timeProvider,
            new AdvancingMockClient(timeProvider, TimeSpan.FromMilliseconds(100)),
            budget: RunBudget.Default with
            {
                MaxRunDuration = TimeSpan.FromMilliseconds(50)
            });

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    "Bound the run duration.",
                    "advancing-mock",
                    workspace.OutputRoot)));

        var (kind, limit, observed) = await ReadBreachAsync(exception.OutputDirectory);
        Assert.Equal("runDurationMs", kind);
        Assert.Equal(50, limit);
        Assert.True(observed > 50, $"observed {observed} should exceed 50");
    }

    [Fact]
    public async Task RunAsync_DefaultBudget_DoesNotInterfere()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(workspace, FixedTime());

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "A normal mock run stays well within the default budget.",
                "mock",
                workspace.OutputRoot));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        var eventTypes = await ReadTraceEventTypesAsync(result.OutputDirectory);
        Assert.DoesNotContain("budget.exceeded", eventTypes);
    }

    private static async Task<(string Kind, long Limit, long Observed)>
        RunAndReadBreachAsync(RunBudget budget, string? input = null)
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(workspace, FixedTime(), budget: budget);

        var exception = await Assert.ThrowsAsync<RuntimeRunException>(
            () => orchestrator.RunAsync(
                new RunRequest(
                    "frame",
                    input ?? "A run that should trip a budget.",
                    "mock",
                    workspace.OutputRoot)));

        await AssertTerminalFailureAsync(exception.OutputDirectory);
        return await ReadBreachAsync(exception.OutputDirectory);
    }

    private static async Task<(string Kind, long Limit, long Observed)>
        ReadBreachAsync(string outputDirectory)
    {
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "trace.json")));
        var breach = document.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .Single(traceEvent =>
                traceEvent.GetProperty("type").GetString() == "budget.exceeded");
        var data = breach.GetProperty("data");
        return (
            data.GetProperty("budgetKind").GetString()!,
            data.GetProperty("limit").GetInt64(),
            data.GetProperty("observed").GetInt64());
    }

    private sealed class AdvancingMockClient(
        FixedTimeProvider timeProvider,
        TimeSpan perCall) : IModelClient
    {
        private readonly MockModelClient _inner = new();

        public string ProviderName => "advancing-mock";

        public async Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _inner.CompleteAsync(request, cancellationToken);
            timeProvider.Advance(perCall);
            return response with { Provider = ProviderName };
        }
    }
}
