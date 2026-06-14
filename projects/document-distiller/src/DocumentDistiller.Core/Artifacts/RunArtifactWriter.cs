using System.Text.Json;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.IO;
using DocumentDistiller.Core.Serialization;

namespace DocumentDistiller.Core.Artifacts;

public sealed class RunArtifactWriter : IRunArtifactWriter
{
    private readonly TimeProvider _timeProvider;

    public RunArtifactWriter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task<RunArtifactPaths> PrepareRunAsync(
        string outputRoot,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();

        var timestamp = _timeProvider
            .GetUtcNow()
            .ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var suffix = runId.Length <= 8 ? runId : runId[..8];
        var runDirectory = Path.GetFullPath(
            Path.Combine(outputRoot, $"{timestamp}_distill_{suffix}"));
        Directory.CreateDirectory(runDirectory);

        return Task.FromResult(
            new RunArtifactPaths(
                runDirectory,
                Path.Combine(runDirectory, "input_manifest.md"),
                Path.Combine(runDirectory, "run_manifest.json"),
                Path.Combine(runDirectory, "source_risk.json"),
                Path.Combine(runDirectory, "evidence.json"),
                Path.Combine(runDirectory, "evidence_matrix.json"),
                Path.Combine(runDirectory, "model_usage.json"),
                Path.Combine(runDirectory, "draft.json"),
                Path.Combine(runDirectory, "critique.json"),
                Path.Combine(runDirectory, "analysis.json"),
                Path.Combine(runDirectory, "report.md"),
                Path.Combine(runDirectory, "index.html"),
                Path.Combine(runDirectory, "trace.json"),
                Path.Combine(runDirectory, "run_summary.md"),
                Path.Combine(runDirectory, "eval_report.md")));
    }

    public Task WriteTextAsync(
        RunArtifactPaths artifacts,
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(content);
        EnsureWithinRunDirectory(artifacts.RunDirectory, path);
        return FilePersistence.WriteAllTextAtomicAsync(
            path,
            content,
            cancellationToken);
    }

    public Task WriteJsonAsync<T>(
        RunArtifactPaths artifacts,
        string path,
        T value,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Options);
        return WriteTextAsync(artifacts, path, json, cancellationToken);
    }

    internal static void EnsureWithinRunDirectory(
        string runDirectory,
        string path)
    {
        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(runDirectory));
        var candidate = Path.GetFullPath(path);
        var prefix = root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Artifact path '{path}' is outside run directory '{runDirectory}'.");
        }
    }
}
