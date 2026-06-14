using System.Text;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Tests;

public sealed class JsonTraceReaderTests
{
    [Fact]
    public async Task ReadAsync_ReadsCurrentSchemaAndContiguousSequences()
    {
        await using var stream = JsonStream(
            """
            {
              "schemaVersion": 1,
              "runId": "run-001",
              "events": [
                {
                  "sequence": 1,
                  "timestamp": "2026-06-14T12:00:00Z",
                  "type": "run.started",
                  "runId": "run-001",
                  "data": {}
                },
                {
                  "sequence": 2,
                  "timestamp": "2026-06-14T12:00:01Z",
                  "type": "run.completed",
                  "runId": "run-001",
                  "data": {}
                }
              ]
            }
            """);

        var document = await new JsonTraceReader().ReadAsync(stream);

        Assert.Equal(TraceSchema.CurrentVersion, document.SchemaVersion);
        Assert.Equal("run-001", document.RunId);
        Assert.Equal([1L, 2L], document.Events.Select(e => e.Sequence).ToArray());
    }

    [Fact]
    public async Task ReadAsync_RejectsUnsupportedFutureSchemaVersion()
    {
        await using var stream = JsonStream(
            $$"""
            {
              "schemaVersion": {{TraceSchema.CurrentVersion + 1}},
              "runId": "run-001",
              "events": []
            }
            """);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => new JsonTraceReader().ReadAsync(stream));

        Assert.Contains("newer than supported", exception.Message);
        Assert.Contains(
            (TraceSchema.CurrentVersion + 1).ToString(),
            exception.Message);
    }

    [Fact]
    public async Task ReadAsync_RejectsNonContiguousSequences()
    {
        await using var stream = JsonStream(
            """
            {
              "schemaVersion": 1,
              "runId": "run-001",
              "events": [
                {
                  "sequence": 2,
                  "timestamp": "2026-06-14T12:00:00Z",
                  "type": "run.started",
                  "runId": "run-001",
                  "data": {}
                }
              ]
            }
            """);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => new JsonTraceReader().ReadAsync(stream));

        Assert.Contains("expected '1' but found '2'", exception.Message);
    }

    private static MemoryStream JsonStream(string json) =>
        new(Encoding.UTF8.GetBytes(json));
}
