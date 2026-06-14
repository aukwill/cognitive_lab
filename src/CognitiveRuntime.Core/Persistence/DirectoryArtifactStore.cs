using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.IO;

namespace CognitiveRuntime.Core.Persistence;

public sealed class DirectoryArtifactStore : IArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory;

    public DirectoryArtifactStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public async Task PutAsync(
        StoredRunArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        StoredRunArtifactValidator.Validate(artifact);

        var contentPath = GetContentPath(
            artifact.RunId,
            artifact.RelativePath);
        var metadataPath = GetMetadataPath(
            artifact.RunId,
            artifact.RelativePath);
        await FilePersistence.WriteAllBytesAtomicAsync(
            contentPath,
            artifact.Content,
            cancellationToken);
        await FilePersistence.WriteAllTextAtomicAsync(
            metadataPath,
            JsonSerializer.Serialize(
                new StoredArtifactMetadata(
                    artifact.SchemaVersion,
                    artifact.RunId,
                    artifact.RelativePath,
                    artifact.MediaType,
                    artifact.ByteLength,
                    artifact.Sha256,
                    artifact.UpdatedAt),
                JsonOptions),
            cancellationToken);
    }

    public async Task<StoredRunArtifact?> GetAsync(
        string runId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        relativePath =
            StoredRunArtifactValidator.NormalizeRelativePath(relativePath);
        var contentPath = GetContentPath(runId, relativePath);
        var metadataPath = GetMetadataPath(runId, relativePath);
        if (!File.Exists(contentPath) || !File.Exists(metadataPath))
        {
            return null;
        }

        var metadata = JsonSerializer.Deserialize<StoredArtifactMetadata>(
            await File.ReadAllTextAsync(metadataPath, cancellationToken),
            JsonOptions)
            ?? throw new InvalidOperationException(
                $"Artifact metadata '{metadataPath}' is empty.");
        var content = await File.ReadAllBytesAsync(
            contentPath,
            cancellationToken);
        var artifact = new StoredRunArtifact(
            metadata.SchemaVersion,
            metadata.RunId,
            metadata.RelativePath,
            metadata.MediaType,
            content,
            metadata.ByteLength,
            metadata.Sha256,
            metadata.UpdatedAt);
        StoredRunArtifactValidator.Validate(artifact);
        return artifact;
    }

    public async Task<IReadOnlyList<StoredRunArtifactDescriptor>> ListAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var metadataDirectory = GetRunMetadataDirectory(runId);
        if (!Directory.Exists(metadataDirectory))
        {
            return [];
        }

        var descriptors = new List<StoredRunArtifactDescriptor>();
        foreach (var path in Directory.EnumerateFiles(
                     metadataDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = JsonSerializer.Deserialize<StoredArtifactMetadata>(
                await File.ReadAllTextAsync(path, cancellationToken),
                JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Artifact metadata '{path}' is empty.");
            descriptors.Add(
                new StoredRunArtifactDescriptor(
                    metadata.RunId,
                    metadata.RelativePath,
                    metadata.MediaType,
                    metadata.ByteLength,
                    metadata.Sha256,
                    metadata.UpdatedAt));
        }

        return descriptors
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private string GetContentPath(string runId, string relativePath)
    {
        var runRoot = GetRunRoot(runId);
        var path = Path.GetFullPath(
            Path.Combine(
                runRoot,
                "objects",
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureWithin(runRoot, path);
        return path;
    }

    private string GetMetadataPath(string runId, string relativePath) =>
        Path.Combine(
            GetRunMetadataDirectory(runId),
            $"{CreatePathKey(relativePath)}.json");

    private string GetRunMetadataDirectory(string runId) =>
        Path.Combine(GetRunRoot(runId), "metadata");

    private string GetRunRoot(string runId)
    {
        var safeRunId = new string(
            runId.Where(character =>
                    char.IsAsciiLetterOrDigit(character) ||
                    character is '-' or '_')
                .ToArray());
        if (!string.Equals(runId, safeRunId, StringComparison.Ordinal) ||
            safeRunId.Length == 0)
        {
            throw new ArgumentException(
                $"Run ID '{runId}' is not a safe storage key.",
                nameof(runId));
        }

        return Path.Combine(_rootDirectory, safeRunId);
    }

    private static string CreatePathKey(string relativePath) =>
        Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        StoredRunArtifactValidator.NormalizeRelativePath(
                            relativePath))))
            .ToLowerInvariant();

    private static void EnsureWithin(string rootDirectory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(rootDirectory));
        var candidate = Path.GetFullPath(path);
        if (!candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Artifact path '{path}' is outside store root '{root}'.");
        }
    }

    private sealed record StoredArtifactMetadata(
        int SchemaVersion,
        string RunId,
        string RelativePath,
        string MediaType,
        long ByteLength,
        string Sha256,
        DateTimeOffset UpdatedAt);
}
