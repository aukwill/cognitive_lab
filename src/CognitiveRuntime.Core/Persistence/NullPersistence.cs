using CognitiveRuntime.Core.Abstractions;

namespace CognitiveRuntime.Core.Persistence;

public sealed class NullRunStateStore : IRunStateStore
{
    public Task UpsertRunAsync(
        RunCatalogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<RunCatalogEntry?> GetRunAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<RunCatalogEntry?>(null);
    }

    public Task<IReadOnlyList<RunCatalogEntry>> ListRunsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RunCatalogEntry>>([]);
    }
}

public sealed class NullArtifactStore : IArtifactStore
{
    public Task PutAsync(
        StoredRunArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();
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
        return Task.FromResult<StoredRunArtifact?>(null);
    }

    public Task<IReadOnlyList<StoredRunArtifactDescriptor>> ListAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<StoredRunArtifactDescriptor>>([]);
    }
}

public sealed class NullSemanticIndex : ISemanticIndex
{
    public Task UpsertAsync(
        IReadOnlyList<SemanticIndexDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SemanticSearchMatch>> SearchAsync(
        SemanticSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<SemanticSearchMatch>>([]);
    }

    public Task DeleteRunAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
