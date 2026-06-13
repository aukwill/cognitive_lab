namespace CognitiveRuntime.Core.Contracts;

public sealed record TraceEvent(
    DateTimeOffset Timestamp,
    string Type,
    string RunId,
    IReadOnlyDictionary<string, object?> Data);

public sealed record TraceDocument(
    string RunId,
    IReadOnlyList<TraceEvent> Events);
