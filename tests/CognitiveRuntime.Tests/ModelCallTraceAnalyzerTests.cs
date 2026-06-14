using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

public sealed class ModelCallTraceAnalyzerTests
{
    [Fact]
    public void Analyze_AcceptsOneCalledAndOneCompletedEvent()
    {
        var analysis = ModelCallTraceAnalyzer.Analyze(
            [
                CreateEvent(1, TraceEventNames.ModelCalled),
                CreateEvent(2, TraceEventNames.ModelCompleted)
            ]);

        Assert.True(analysis.IsValid);
        var call = Assert.Single(analysis.Calls);
        Assert.Equal("call-001", call.CallId);
        Assert.Equal("main", call.NodeId);
        Assert.Equal(1, call.Attempt);
        Assert.Equal(1, call.CalledCount);
        Assert.Equal(1, call.CompletedCount);
        Assert.Equal(0, call.FailedCount);
    }

    [Fact]
    public void Analyze_DetectsMissingTerminalEvent()
    {
        var analysis = ModelCallTraceAnalyzer.Analyze(
            [CreateEvent(1, TraceEventNames.ModelCalled)]);

        Assert.False(analysis.IsValid);
        Assert.Contains(
            analysis.Issues,
            issue => issue.Contains(
                "0 terminal events; expected 1",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DetectsDuplicateTerminalEvents()
    {
        var analysis = ModelCallTraceAnalyzer.Analyze(
            [
                CreateEvent(1, TraceEventNames.ModelCalled),
                CreateEvent(2, TraceEventNames.ModelCompleted),
                CreateEvent(3, TraceEventNames.ModelCompleted)
            ]);

        Assert.False(analysis.IsValid);
        Assert.Contains(
            analysis.Issues,
            issue => issue.Contains(
                "2 terminal events; expected 1",
                StringComparison.Ordinal));
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
