namespace CognitiveRuntime.Core.Persistence;

public sealed record StoredRunArtifact(
    int SchemaVersion,
    string RunId,
    string RelativePath,
    string MediaType,
    byte[] Content,
    long ByteLength,
    string Sha256,
    DateTimeOffset UpdatedAt);

public sealed record StoredRunArtifactDescriptor(
    string RunId,
    string RelativePath,
    string MediaType,
    long ByteLength,
    string Sha256,
    DateTimeOffset UpdatedAt);
