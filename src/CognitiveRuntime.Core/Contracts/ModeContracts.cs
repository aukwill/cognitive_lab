namespace CognitiveRuntime.Core.Contracts;

public enum PhaseKind
{
    Main,
    Critic
}

public sealed record ModeManifest
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public List<ModePhaseManifest> Phases { get; init; } = [];

    public OutputContract OutputContract { get; init; } = new();
}

public sealed record ModePhaseManifest
{
    public string Name { get; init; } = string.Empty;

    public PhaseKind Kind { get; init; }

    public string Prompt { get; init; } = string.Empty;
}

public sealed record OutputContract
{
    public List<string> RequiredHeadings { get; init; } = [];

    public int MinimumLength { get; init; } = 1;
}

public sealed record LoadedMode(
    string DirectoryPath,
    string Documentation,
    ModeManifest Manifest,
    IReadOnlyList<LoadedPhase> Phases);

public sealed record LoadedPhase(string Name, PhaseKind Kind, string Prompt);
