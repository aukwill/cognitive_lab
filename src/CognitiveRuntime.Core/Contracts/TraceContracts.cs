namespace CognitiveRuntime.Core.Contracts;

public static class TraceSchema
{
    public const int CurrentVersion = 1;
}

public sealed record TraceEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    string Type,
    string RunId,
    IReadOnlyDictionary<string, object?> Data);

public sealed record TraceDocument(
    int SchemaVersion,
    string RunId,
    IReadOnlyList<TraceEvent> Events);
