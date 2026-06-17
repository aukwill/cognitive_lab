using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using static CognitiveRuntime.Tests.RuntimeTestHarness;

namespace CognitiveRuntime.Tests;

public sealed class TracePayloadContractsTests
{
    [Fact]
    public void PayloadKeys_MatchTheSerializedJsonNames()
    {
        // The contract type carries the exact JSON keys written to trace.json,
        // so serialized data stays plain and exposes no implementation names.
        Assert.Equal("nodeId", TracePayloadKeys.NodeId);
        Assert.Equal("stageId", TracePayloadKeys.StageId);
        Assert.Equal("phase", TracePayloadKeys.Phase);
        Assert.Equal("callId", TracePayloadKeys.CallId);
        Assert.Equal("attempt", TracePayloadKeys.Attempt);
        Assert.Equal("contextNodeIds", TracePayloadKeys.ContextNodeIds);
        Assert.Equal("outcome", TracePayloadKeys.Outcome);
        Assert.Equal("durationMs", TracePayloadKeys.DurationMs);
        Assert.Equal("name", TracePayloadKeys.Name);
        Assert.Equal("kind", TracePayloadKeys.Kind);
    }

    [Fact]
    public async Task CoreTraceEvents_CarryTheirRequiredPayloadKeys()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var orchestrator = CreateOrchestrator(
            workspace,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero)));

        var result = await orchestrator.RunAsync(
            new RunRequest(
                "frame",
                "Exercise the core trace payload contract.",
                "mock",
                workspace.OutputRoot));

        await using var traceStream = File.OpenRead(result.TracePath);
        using var traceDocument = await JsonDocument.ParseAsync(traceStream);
        var events = traceDocument.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .ToArray();

        // The keys evals and views read must be present on the events that
        // produce them, all referenced through the shared contract.
        AssertEventKeys(events, TraceEventNames.NodeCompleted,
            TracePayloadKeys.NodeId,
            TracePayloadKeys.Phase,
            TracePayloadKeys.PhaseKind,
            TracePayloadKeys.Status,
            TracePayloadKeys.ContextNodeIds,
            TracePayloadKeys.Dependencies,
            TracePayloadKeys.DurationMs);
        AssertEventKeys(events, TraceEventNames.ModelCompleted,
            TracePayloadKeys.CallId,
            TracePayloadKeys.NodeId,
            TracePayloadKeys.Attempt,
            TracePayloadKeys.Provider,
            TracePayloadKeys.Phase,
            TracePayloadKeys.ContentLength,
            TracePayloadKeys.DurationMs);
        AssertEventKeys(events, TraceEventNames.PhaseCompleted,
            TracePayloadKeys.NodeId,
            TracePayloadKeys.Mode,
            TracePayloadKeys.Phase,
            TracePayloadKeys.Kind,
            TracePayloadKeys.DurationMs);
        AssertEventKeys(events, TraceEventNames.ArtifactWritten,
            TracePayloadKeys.Name,
            TracePayloadKeys.Kind);
        AssertEventKeys(events, TraceEventNames.EvalCompleted,
            TracePayloadKeys.Passed,
            TracePayloadKeys.CheckCount,
            TracePayloadKeys.DurationMs);
        AssertEventKeys(events, TraceEventNames.RunFinalized,
            TracePayloadKeys.FromStatus,
            TracePayloadKeys.LifecycleStatus,
            TracePayloadKeys.Outcome,
            TracePayloadKeys.DurationMs);
    }

    private static void AssertEventKeys(
        IReadOnlyList<JsonElement> events,
        string eventType,
        params string[] requiredKeys)
    {
        var matches = events
            .Where(traceEvent =>
                traceEvent.GetProperty("type").GetString() == eventType)
            .ToArray();
        Assert.NotEmpty(matches);

        foreach (var match in matches)
        {
            var data = match.GetProperty("data");
            foreach (var key in requiredKeys)
            {
                Assert.True(
                    data.TryGetProperty(key, out _),
                    $"Event '{eventType}' is missing payload key '{key}'.");
            }
        }
    }
}
