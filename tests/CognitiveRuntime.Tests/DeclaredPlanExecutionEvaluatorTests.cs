using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class DeclaredPlanExecutionEvaluatorTests
{
    private readonly DeclaredPlanExecutionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_AcceptsDeclaredOrderContextAndAuthority()
    {
        var fixture = CreateFixture();

        var result = _evaluator.Evaluate(
            fixture.Plan,
            fixture.Events,
            fixture.Results,
            "final");

        Assert.True(result.Passed, result.Details);
    }

    [Fact]
    public void Evaluate_RejectsDuplicateAndUndeclaredTerminalEvents()
    {
        var fixture = CreateFixture();
        var events = fixture.Events.Concat(
            [
                NodeEvent(
                    7,
                    TraceEventNames.NodeCompleted,
                    "main",
                    []),
                NodeEvent(
                    8,
                    TraceEventNames.NodeCompleted,
                    "undeclared",
                    [])
            ]).ToArray();

        var result = _evaluator.Evaluate(
            fixture.Plan,
            events,
            fixture.Results,
            "final");

        Assert.False(result.Passed);
        Assert.Contains("main' has 2 terminal events", result.Details);
        Assert.Contains("Undeclared node 'undeclared'", result.Details);
    }

    [Fact]
    public void Evaluate_RejectsMissingTerminalEvent()
    {
        var fixture = CreateFixture();
        var events = fixture.Events
            .Where(traceEvent =>
                !(traceEvent.Type == TraceEventNames.NodeCompleted &&
                  GetNodeId(traceEvent) == "revision"))
            .ToArray();

        var result = _evaluator.Evaluate(
            fixture.Plan,
            events,
            fixture.Results,
            "final");

        Assert.False(result.Passed);
        Assert.Contains(
            "revision' has 0 terminal events; expected 1",
            result.Details);
    }

    [Fact]
    public void Evaluate_RejectsDependencyOrderAndContextDrift()
    {
        var fixture = CreateFixture();
        var events = new[]
        {
            NodeEvent(1, TraceEventNames.NodeStarted, "main", []),
            NodeEvent(2, TraceEventNames.NodeStarted, "critic", ["main"]),
            NodeEvent(3, TraceEventNames.NodeCompleted, "main", []),
            NodeEvent(4, TraceEventNames.NodeCompleted, "critic", []),
            NodeEvent(
                5,
                TraceEventNames.NodeStarted,
                "revision",
                ["main", "critic"]),
            NodeEvent(
                6,
                TraceEventNames.NodeCompleted,
                "revision",
                ["main", "critic"])
        };

        var result = _evaluator.Evaluate(
            fixture.Plan,
            events,
            fixture.Results,
            "final");

        Assert.False(result.Passed);
        Assert.Contains(
            "critic' started before dependency 'main'",
            result.Details);
        Assert.Contains(
            "critic' context was []; expected [main]",
            result.Details);
    }

    [Fact]
    public void Evaluate_RejectsAuthoritativeResultMismatch()
    {
        var fixture = CreateFixture();

        var result = _evaluator.Evaluate(
            fixture.Plan,
            fixture.Events,
            fixture.Results,
            "different");

        Assert.False(result.Passed);
        Assert.Contains(
            "does not match the content used for output-contract evaluation",
            result.Details);
    }

    private static ExecutionFixture CreateFixture()
    {
        var plan = new EvalExecutionPlan(
            [
                new EvalExecutionNode("main", [], [], null),
                new EvalExecutionNode("critic", ["main"], ["main"], null),
                new EvalExecutionNode(
                    "revision",
                    ["main", "critic"],
                    ["main", "critic"],
                    null)
            ],
            "revision");
        var events = new[]
        {
            NodeEvent(1, TraceEventNames.NodeStarted, "main", []),
            NodeEvent(2, TraceEventNames.NodeCompleted, "main", []),
            NodeEvent(3, TraceEventNames.NodeStarted, "critic", ["main"]),
            NodeEvent(4, TraceEventNames.NodeCompleted, "critic", ["main"]),
            NodeEvent(
                5,
                TraceEventNames.NodeStarted,
                "revision",
                ["main", "critic"]),
            NodeEvent(
                6,
                TraceEventNames.NodeCompleted,
                "revision",
                ["main", "critic"])
        };
        var results = new Dictionary<string, PhaseResult>(
            StringComparer.Ordinal)
        {
            ["main"] = PhaseResult("main", PhaseKind.Main, "draft"),
            ["critic"] = PhaseResult("critic", PhaseKind.Critic, "review"),
            ["revision"] = PhaseResult(
                "revision",
                PhaseKind.Revision,
                "final")
        };

        return new ExecutionFixture(plan, events, results);
    }

    private static TraceEvent NodeEvent(
        long sequence,
        string type,
        string nodeId,
        IReadOnlyList<string> contextNodeIds) =>
        new(
            sequence,
            DateTimeOffset.UnixEpoch,
            type,
            "run-001",
            new Dictionary<string, object?>
            {
                ["nodeId"] = nodeId,
                ["contextNodeIds"] = contextNodeIds
            });

    private static PhaseResult PhaseResult(
        string name,
        PhaseKind kind,
        string content) =>
        new(name, kind, content, "mock", "test");

    private static string? GetNodeId(TraceEvent traceEvent) =>
        traceEvent.Data["nodeId"]?.ToString();

    private sealed record ExecutionFixture(
        EvalExecutionPlan Plan,
        IReadOnlyList<TraceEvent> Events,
        IReadOnlyDictionary<string, PhaseResult> Results);
}
