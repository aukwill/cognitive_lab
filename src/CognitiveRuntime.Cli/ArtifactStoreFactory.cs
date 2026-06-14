using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Persistence;
using Microsoft.Extensions.Configuration;

namespace CognitiveRuntime.Cli;

internal static class ArtifactStoreFactory
{
    private const string ProviderKey =
        "COGNITIVE_RUNTIME_ARTIFACT_PROVIDER";
    private const string RootKey =
        "COGNITIVE_RUNTIME_ARTIFACT_ROOT";

    public static IArtifactStore Create(
        string outputRoot,
        IConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration[ProviderKey]?.Trim();
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(
                provider,
                "disabled",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                provider,
                "none",
                StringComparison.OrdinalIgnoreCase))
        {
            return new NullArtifactStore();
        }

        if (!string.Equals(
                provider,
                "directory",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unknown artifact provider '{provider}'. Supported values: " +
                "disabled, directory.");
        }

        var configuredRoot = configuration[RootKey];
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetFullPath(outputRoot), ".artifact-store")
            : configuredRoot;
        return new DirectoryArtifactStore(root);
    }
}
