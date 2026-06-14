using System.Security.Cryptography;
using System.Text;

namespace CognitiveRuntime.Core.Persistence;

internal static class StoredRunArtifactFactory
{
    public const int CurrentSchemaVersion = 1;

    public static StoredRunArtifact CreateText(
        string runId,
        string runRootDirectory,
        string path,
        string mediaType,
        string content,
        DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentNullException.ThrowIfNull(content);

        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(runRootDirectory));
        var fullPath = Path.GetFullPath(path);
        var rootPrefix = root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Artifact path '{path}' is outside run root '{runRootDirectory}'.");
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        return new StoredRunArtifact(
            CurrentSchemaVersion,
            runId,
            StoredRunArtifactValidator.NormalizeRelativePath(
                Path.GetRelativePath(root, fullPath)),
            mediaType,
            bytes,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            updatedAt);
    }
}

internal static class StoredRunArtifactValidator
{
    public static void Validate(StoredRunArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifact.RunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifact.RelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifact.MediaType);

        var normalizedPath = NormalizeRelativePath(artifact.RelativePath);
        if (!string.Equals(
                normalizedPath,
                artifact.RelativePath,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Stored artifact paths must use normalized forward slashes.",
                nameof(artifact));
        }

        if (artifact.ByteLength != artifact.Content.LongLength)
        {
            throw new ArgumentException(
                "Stored artifact byte length does not match its content.",
                nameof(artifact));
        }

        var actualSha256 = Convert
            .ToHexString(SHA256.HashData(artifact.Content))
            .ToLowerInvariant();
        if (!string.Equals(
                actualSha256,
                artifact.Sha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Stored artifact SHA-256 does not match its content.",
                nameof(artifact));
        }
    }

    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException(
                $"Artifact path '{relativePath}' is not a safe relative path.",
                nameof(relativePath));
        }

        return normalized;
    }
}
