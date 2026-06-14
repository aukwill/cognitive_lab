namespace CognitiveRuntime.Core.Persistence;

public sealed record SemanticIndexDocument(
    string Id,
    string RunId,
    string SourcePath,
    string Content,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<float>? Embedding = null);

public sealed record SemanticSearchQuery(
    string Text,
    int MaxResults,
    string? RunId = null,
    IReadOnlyList<float>? Embedding = null);

public sealed record SemanticSearchMatch(
    string DocumentId,
    string RunId,
    string SourcePath,
    string Content,
    double Score,
    IReadOnlyDictionary<string, string> Metadata);
