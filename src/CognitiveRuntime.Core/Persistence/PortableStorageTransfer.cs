using CognitiveRuntime.Core.Abstractions;

namespace CognitiveRuntime.Core.Persistence;

public sealed record PortableStorageTransferResult(
    int RunCount,
    int ArtifactCount);

public static class PortableStorageTransfer
{
    public static async Task<PortableStorageTransferResult> CopyAsync(
        IRunStateStore sourceRunState,
        IArtifactStore sourceArtifacts,
        IRunStateStore destinationRunState,
        IArtifactStore destinationArtifacts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceRunState);
        ArgumentNullException.ThrowIfNull(sourceArtifacts);
        ArgumentNullException.ThrowIfNull(destinationRunState);
        ArgumentNullException.ThrowIfNull(destinationArtifacts);

        var runs = await sourceRunState.ListRunsAsync(cancellationToken);
        var artifactCount = 0;
        foreach (var run in runs)
        {
            await destinationRunState.UpsertRunAsync(
                run,
                cancellationToken);
            artifactCount += await CopyArtifactsAsync(
                run.RunId,
                sourceArtifacts,
                destinationArtifacts,
                cancellationToken);
        }

        return new PortableStorageTransferResult(
            runs.Count,
            artifactCount);
    }

    public static async Task<int> CopyArtifactsAsync(
        string runId,
        IArtifactStore source,
        IArtifactStore destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var descriptors = await source.ListAsync(
            runId,
            cancellationToken);
        foreach (var descriptor in descriptors)
        {
            var artifact = await source.GetAsync(
                runId,
                descriptor.RelativePath,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Source artifact '{descriptor.RelativePath}' for run " +
                    $"'{runId}' disappeared during transfer.");
            await destination.PutAsync(artifact, cancellationToken);
        }

        return descriptors.Count;
    }
}
