namespace CognitiveRuntime.Core.Experiments.Dungeons;

public sealed class DungeonExperimentDefinitionLoader
{
    public async Task<DungeonExperimentPrompts> LoadAsync(
        string experimentsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentsRoot);
        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(experimentsRoot));
        var experimentDirectory = ResolveChild(root, "dungeon-builder");
        var proposePath = ResolveChild(
            experimentDirectory,
            Path.Combine("prompts", "propose.md"));
        var revisePath = ResolveChild(
            experimentDirectory,
            Path.Combine("prompts", "revise.md"));

        if (!File.Exists(proposePath) || !File.Exists(revisePath))
        {
            throw new InvalidOperationException(
                $"Dungeon builder prompts were not found under '{experimentDirectory}'.");
        }

        var propose = await File.ReadAllTextAsync(proposePath, cancellationToken);
        var revise = await File.ReadAllTextAsync(revisePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(propose) || string.IsNullOrWhiteSpace(revise))
        {
            throw new InvalidOperationException(
                "Dungeon builder proposal and revision prompts must be non-empty.");
        }

        return new DungeonExperimentPrompts(propose, revise);
    }

    private static string ResolveChild(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                $"Experiment path '{relativePath}' must be relative.");
        }

        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Experiment path '{relativePath}' escapes '{root}'.");
        }

        return candidate;
    }
}
