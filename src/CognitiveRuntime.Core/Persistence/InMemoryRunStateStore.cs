using System.Collections.Concurrent;
using CognitiveRuntime.Core.Abstractions;

namespace CognitiveRuntime.Core.Persistence;

public sealed class InMemoryRunStateStore : IRunStateStore
{
    private readonly ConcurrentDictionary<string, RunCatalogEntry> _entries =
        new(StringComparer.Ordinal);

    public Task UpsertRunAsync(
        RunCatalogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        _entries.AddOrUpdate(
            entry.RunId,
            entry,
            (_, current) =>
                current.Generation <= entry.Generation ? entry : current);
        return Task.CompletedTask;
    }

    public Task<RunCatalogEntry?> GetRunAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryGetValue(runId, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<RunCatalogEntry>> ListRunsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RunCatalogEntry> entries = _entries
            .Values
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.RunId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(entries);
    }
}
