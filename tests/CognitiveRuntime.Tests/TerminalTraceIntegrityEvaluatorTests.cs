using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Evaluation;

namespace CognitiveRuntime.Tests;

public sealed class TerminalTraceIntegrityEvaluatorTests
{
    private readonly TerminalTraceIntegrityEvaluator _evaluator = new();

    [Theory]
    [InlineData(TraceEventNames.RunFinalized)]
    [InlineData(TraceEventNames.RunFailed)]
    [InlineData(TraceEventNames.RunCancelled)]
    public void Evaluate_PassesWhenOneTerminalEventIsLast(string terminalType)
    {
        var result = _evaluator.Evaluate(
            [
                CreateEvent(1, TraceEventNames.RunStarted),
                CreateEvent(2, terminalType)
            ]);

        Assert.True(result.Passed, result.Details);
    }

    [Fact]
    public void Evaluate_RejectsMissingTerminalEvent()
    {
        var result = _evaluator.Evaluate(
            [CreateEvent(1, TraceEventNames.RunStarted)]);

        Assert.False(result.Passed);
        Assert.Contains("no terminal event", result.Details);
    }

    [Fact]
    public void Evaluate_RejectsEventsAfterTerminalEvent()
    {
        var result = _evaluator.Evaluate(
            [
                CreateEvent(1, TraceEventNames.RunStarted),
                CreateEvent(2, TraceEventNames.RunCancelled),
                CreateEvent(3, TraceEventNames.ArtifactWritten)
            ]);

        Assert.False(result.Passed);
        Assert.Contains("1 event(s) after terminal event", result.Details);
    }

    [Fact]
    public void Evaluate_RejectsLaterSuccessAfterFailure()
    {
        var result = _evaluator.Evaluate(
            [
                CreateEvent(1, TraceEventNames.RunStarted),
                CreateEvent(2, TraceEventNames.RunFailed),
                CreateEvent(3, TraceEventNames.RunFinalized)
            ]);

        Assert.False(result.Passed);
        Assert.Contains("2 terminal events", result.Details);
        Assert.Contains("later success event", result.Details);
    }

    private static TraceEvent CreateEvent(long sequence, string type) =>
        new(
            sequence,
            DateTimeOffset.UnixEpoch,
            type,
            "run-001",
            new Dictionary<string, object?>());
}
