using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

public sealed class JsonTraceSessionTests
{
    [Fact]
    public async Task EmitAsync_RejectsEventsAfterTerminalEvent()
    {
        using var workspace = new TestWorkspace();
        var tracePath = Path.Combine(workspace.Root, "trace.json");
        var trace = await new JsonTraceSessionFactory(TimeProvider.System)
            .CreateAsync("run-001", tracePath);
        await trace.EmitAsync(TraceEventNames.RunStarted);
        await trace.EmitAsync(TraceEventNames.RunFinalized);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => trace.EmitAsync(TraceEventNames.ArtifactWritten));

        Assert.Contains("already terminal", exception.Message);
        var document = await new JsonTraceReader().ReadAsync(tracePath);
        Assert.Equal(
            [TraceEventNames.RunStarted, TraceEventNames.RunFinalized],
            document.Events.Select(traceEvent => traceEvent.Type));
    }
}
