using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.IO;

namespace CognitiveRuntime.Core.Artifacts;

public sealed class ArtifactWriter : IArtifactWriter
{
    private readonly TimeProvider _timeProvider;

    public ArtifactWriter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task<RunArtifactPaths> PrepareRunAsync(
        string outputRoot,
        string modeName,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();

        var safeModeName = SanitizeModeName(modeName);
        var runIdSuffix = runId.Length <= 8 ? runId : runId[..8];
        var timestamp = _timeProvider
            .GetUtcNow()
            .ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var runDirectory = Path.GetFullPath(
            Path.Combine(outputRoot, $"{timestamp}_{safeModeName}_{runIdSuffix}"));

        Directory.CreateDirectory(runDirectory);

        var artifacts = new RunArtifactPaths(
            runDirectory,
            Path.Combine(runDirectory, "input.md"),
            Path.Combine(runDirectory, "result.md"),
            Path.Combine(runDirectory, "trace.json"),
            Path.Combine(runDirectory, "run_summary.md"),
            Path.Combine(runDirectory, "eval_report.md"),
            Path.Combine(runDirectory, "pattern.md"));

        return Task.FromResult(artifacts);
    }

    public Task<RunArtifactPaths> PrepareStageAsync(
        RunArtifactPaths root,
        int stageIndex,
        string modeName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);
        cancellationToken.ThrowIfCancellationRequested();

        var safeModeName = SanitizeModeName(modeName);
        var stageDirectory = Path.GetFullPath(
            Path.Combine(
                root.RunDirectory,
                "stages",
                $"{stageIndex:D2}-{safeModeName}"));

        EnsureWithinRunDirectory(root.RunDirectory, stageDirectory);
        Directory.CreateDirectory(stageDirectory);

        var artifacts = new RunArtifactPaths(
            stageDirectory,
            Path.Combine(stageDirectory, "input.md"),
            Path.Combine(stageDirectory, "result.md"),
            Path.Combine(stageDirectory, "trace.json"),
            Path.Combine(stageDirectory, "run_summary.md"),
            Path.Combine(stageDirectory, "eval_report.md"),
            Path.Combine(stageDirectory, "pattern.md"));

        return Task.FromResult(artifacts);
    }

    private static string SanitizeModeName(string modeName)
    {
        var safeModeName = new string(
            modeName
                .Where(character =>
                    char.IsAsciiLetterOrDigit(character) || character == '-')
                .ToArray());

        if (safeModeName.Length == 0)
        {
            throw new ArgumentException(
                "Mode name does not contain a safe path component.",
                nameof(modeName));
        }

        return safeModeName;
    }

    public Task WriteAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(content);

        var path = artifacts.GetPath(kind);
        EnsureWithinRunDirectory(artifacts.RunDirectory, path);

        return FilePersistence.WriteAllTextAtomicAsync(
            path,
            content,
            cancellationToken);
    }

    internal static void EnsureWithinRunDirectory(string runDirectory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(runDirectory));
        var candidate = Path.GetFullPath(path);
        var rootPrefix = root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Artifact path '{path}' is outside run directory '{runDirectory}'.");
        }
    }
}
