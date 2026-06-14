using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class ModelCallPairingEvaluatorTests
{
    private readonly ModelCallPairingEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_PassesForOneCalledAndOneCompletion()
    {
        var result = _evaluator.Evaluate(
            [
                CreateEvent(1, TraceEventNames.ModelCalled),
                CreateEvent(2, TraceEventNames.ModelCompleted)
            ]);

        Assert.True(result.Passed, result.Details);
    }

    [Fact]
    public void Evaluate_ReportsUnmatchedCallId()
    {
        var result = _evaluator.Evaluate(
            [CreateEvent(1, TraceEventNames.ModelCalled)]);

        Assert.False(result.Passed);
        Assert.Contains("call-001", result.Details);
        Assert.Contains("0 terminal events", result.Details);
    }

    [Fact]
    public void Evaluate_ReportsDuplicateCompletionForCallId()
    {
        var result = _evaluator.Evaluate(
            [
                CreateEvent(1, TraceEventNames.ModelCalled),
                CreateEvent(2, TraceEventNames.ModelCompleted),
                CreateEvent(3, TraceEventNames.ModelCompleted)
            ]);

        Assert.False(result.Passed);
        Assert.Contains("call-001", result.Details);
        Assert.Contains("2 terminal events", result.Details);
    }

    [Fact]
    public void Evaluate_ReportsCompletionBeforeCalledEvent()
    {
        var result = _evaluator.Evaluate(
            [
                CreateEvent(1, TraceEventNames.ModelCompleted),
                CreateEvent(2, TraceEventNames.ModelCalled)
            ]);

        Assert.False(result.Passed);
        Assert.Contains("completed before its called event", result.Details);
    }

    private static TraceEvent CreateEvent(long sequence, string type) =>
        new(
            sequence,
            DateTimeOffset.UnixEpoch,
            type,
            "run-001",
            new Dictionary<string, object?>
            {
                ["callId"] = "call-001",
                ["nodeId"] = "main",
                ["attempt"] = 1
            });
}
