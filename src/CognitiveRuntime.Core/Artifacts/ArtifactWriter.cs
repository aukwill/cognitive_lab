using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.IO;
using CognitiveRuntime.Core.Persistence;

namespace CognitiveRuntime.Core.Artifacts;

public sealed class ArtifactWriter : IArtifactWriter
{
    private readonly TimeProvider _timeProvider;
    private readonly IArtifactStore _artifactStore;

    public ArtifactWriter(
        TimeProvider timeProvider,
        IArtifactStore? artifactStore = null)
    {
        _timeProvider = timeProvider;
        _artifactStore = artifactStore ?? new NullArtifactStore();
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
        var safeRunId = ValidateRunId(runId);
        var timestamp = _timeProvider
            .GetUtcNow()
            .ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var runDirectory = Path.GetFullPath(
            Path.Combine(outputRoot, $"{timestamp}_{safeModeName}_{safeRunId}"));

        if (Directory.Exists(runDirectory))
        {
            throw new IOException(
                $"Run output directory '{runDirectory}' already exists.");
        }
        Directory.CreateDirectory(runDirectory);

        var artifacts = new RunArtifactPaths(
            runDirectory,
            Path.Combine(runDirectory, "input.md"),
            Path.Combine(runDirectory, "result.md"),
            Path.Combine(runDirectory, "trace.json"),
            Path.Combine(runDirectory, "run_summary.md"),
            Path.Combine(runDirectory, "eval_report.md"),
            Path.Combine(runDirectory, "pattern.md"))
        {
            RunId = runId,
            RootDirectory = runDirectory
        };

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
            Path.Combine(stageDirectory, "pattern.md"))
        {
            RunId = root.RunId,
            RootDirectory = root.RootDirectory
        };

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

    public async Task<string> WritePhaseAsync(
        RunArtifactPaths artifacts,
        string baseDirectory,
        int index,
        string phaseName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(index);

        var safePhaseName = SanitizePhaseName(phaseName);
        var phasesDirectory = Path.GetFullPath(
            Path.Combine(baseDirectory, "phases"));
        var path = Path.Combine(
            phasesDirectory,
            $"{index:D2}-{safePhaseName}.md");

        // Both the phases directory and the file must stay inside the run
        // directory, even when baseDirectory is an attacker-influenced value.
        EnsureWithinRunDirectory(artifacts.RunDirectory, phasesDirectory);
        EnsureWithinRunDirectory(artifacts.RunDirectory, path);
        Directory.CreateDirectory(phasesDirectory);

        await FilePersistence.WriteAllTextAtomicAsync(
            path,
            content,
            cancellationToken);
        await _artifactStore.PutAsync(
            StoredRunArtifactFactory.CreateText(
                artifacts.RunId,
                artifacts.RootDirectory,
                path,
                "text/markdown; charset=utf-8",
                content,
                _timeProvider.GetUtcNow()),
            cancellationToken);
        return path;
    }

    private static string SanitizePhaseName(string phaseName)
    {
        var safePhaseName = new string(
            phaseName
                .Where(character =>
                    char.IsAsciiLetterOrDigit(character) || character == '-')
                .ToArray());

        if (safePhaseName.Length == 0)
        {
            throw new ArgumentException(
                "Phase name does not contain a safe path component.",
                nameof(phaseName));
        }

        return safePhaseName;
    }

    private static string ValidateRunId(string runId)
    {
        if (runId.Length > 64 ||
            runId.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException(
                "Run ID must contain at most 64 ASCII letters, digits, or hyphens.",
                nameof(runId));
        }

        return runId;
    }

    public async Task WriteAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(content);

        var path = artifacts.GetPath(kind);
        EnsureWithinRunDirectory(artifacts.RunDirectory, path);

        await FilePersistence.WriteAllTextAtomicAsync(
            path,
            content,
            cancellationToken);
        await _artifactStore.PutAsync(
            StoredRunArtifactFactory.CreateText(
                artifacts.RunId,
                artifacts.RootDirectory,
                path,
                kind == ArtifactKind.RunManifest
                    ? "application/json; charset=utf-8"
                    : "text/markdown; charset=utf-8",
                content,
                _timeProvider.GetUtcNow()),
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
