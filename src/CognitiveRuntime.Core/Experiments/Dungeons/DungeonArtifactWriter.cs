using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.IO;
using CognitiveRuntime.Core.Persistence;
using System.Security.Cryptography;

namespace CognitiveRuntime.Core.Experiments.Dungeons;

public sealed class DungeonArtifactWriter
{
    private readonly IArtifactStore _artifactStore;
    private readonly TimeProvider _timeProvider;

    public DungeonArtifactWriter(
        IArtifactStore? artifactStore = null,
        TimeProvider? timeProvider = null)
    {
        _artifactStore = artifactStore ?? new NullArtifactStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> WriteTextAsync(
        RunArtifactPaths artifacts,
        string relativePath,
        string content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(artifacts, relativePath);
        await FilePersistence.WriteAllTextAtomicAsync(
            path,
            content,
            cancellationToken);
        await _artifactStore.PutAsync(
            StoredRunArtifactFactory.CreateText(
                artifacts.RunId,
                artifacts.RootDirectory,
                path,
                mediaType,
                content,
                _timeProvider.GetUtcNow()),
            cancellationToken);
        return path;
    }

    public async Task<string> CopyAsync(
        RunArtifactPaths artifacts,
        string relativePath,
        string sourcePath,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(artifacts, relativePath);
        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        await FilePersistence.WriteAllBytesAtomicAsync(
            path,
            bytes,
            cancellationToken);
        await _artifactStore.PutAsync(
            new StoredRunArtifact(
                StoredRunArtifactFactory.CurrentSchemaVersion,
                artifacts.RunId,
                Path.GetRelativePath(artifacts.RootDirectory, path)
                    .Replace('\\', '/'),
                mediaType,
                bytes,
                bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                _timeProvider.GetUtcNow()),
            cancellationToken);
        return path;
    }

    private static string ResolvePath(
        RunArtifactPaths artifacts,
        string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                $"Dungeon artifact path '{relativePath}' must be relative.");
        }

        var path = Path.GetFullPath(
            Path.Combine(artifacts.RunDirectory, relativePath));
        ArtifactWriter.EnsureWithinRunDirectory(artifacts.RunDirectory, path);
        return path;
    }
}
