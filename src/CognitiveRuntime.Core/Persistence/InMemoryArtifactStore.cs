using System.Collections.Concurrent;
using CognitiveRuntime.Core.Abstractions;

namespace CognitiveRuntime.Core.Persistence;

public sealed class InMemoryArtifactStore : IArtifactStore
{
    private readonly ConcurrentDictionary<string, StoredRunArtifact> _artifacts =
        new(StringComparer.Ordinal);

    public Task PutAsync(
        StoredRunArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        StoredRunArtifactValidator.Validate(artifact);
        cancellationToken.ThrowIfCancellationRequested();
        _artifacts[CreateKey(artifact.RunId, artifact.RelativePath)] =
            artifact with
            {
                Content = [.. artifact.Content]
            };
        return Task.CompletedTask;
    }

    public Task<StoredRunArtifact?> GetAsync(
        string runId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        cancellationToken.ThrowIfCancellationRequested();

        _artifacts.TryGetValue(
            CreateKey(
                runId,
                StoredRunArtifactValidator.NormalizeRelativePath(relativePath)),
            out var artifact);
        return Task.FromResult(
            artifact is null
                ? null
                : artifact with
                {
                    Content = [.. artifact.Content]
                });
    }

    public Task<IReadOnlyList<StoredRunArtifactDescriptor>> ListAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<StoredRunArtifactDescriptor> descriptors = _artifacts
            .Values
            .Where(artifact =>
                string.Equals(
                    artifact.RunId,
                    runId,
                    StringComparison.Ordinal))
            .OrderBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .Select(artifact => new StoredRunArtifactDescriptor(
                artifact.RunId,
                artifact.RelativePath,
                artifact.MediaType,
                artifact.ByteLength,
                artifact.Sha256,
                artifact.UpdatedAt))
            .ToArray();
        return Task.FromResult(descriptors);
    }

    private static string CreateKey(string runId, string relativePath) =>
        $"{runId}\0{relativePath}";
}
