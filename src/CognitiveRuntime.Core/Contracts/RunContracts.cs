namespace CognitiveRuntime.Core.Contracts;

public sealed record RunRequest(
    string ModeName,
    string Input,
    string ModelProvider,
    string OutputRoot,
    bool WriteHtmlView = false,
    string? InputSource = null,
    string? Lens = null,
    string Pattern = "critic-revision",
    IReadOnlyList<string>? PipelineStages = null);

/// <summary>
/// The terminal outcome of a run. The runtime, not the model, decides this
/// value.
/// </summary>
public enum RunOutcome
{
    /// <summary>The run completed and its evaluation passed.</summary>
    Success,

    /// <summary>The run completed but its evaluation failed.</summary>
    EvalFailed,

    /// <summary>The run did not complete due to a runtime or provider failure.</summary>
    RuntimeFailed,

    /// <summary>The run was cancelled before it completed.</summary>
    Cancelled
}

public sealed record RunResult(
    string RunId,
    string OutputDirectory,
    string ResultPath,
    string TracePath,
    string EvalReportPath,
    RunOutcome Outcome,
    string? HtmlViewPath = null);

public enum ArtifactKind
{
    Input,
    Result,
    RunSummary,
    EvalReport,
    Pattern,
    RunManifest
}

public sealed record RunArtifactPaths(
    string RunDirectory,
    string InputPath,
    string ResultPath,
    string TracePath,
    string RunSummaryPath,
    string EvalReportPath,
    string PatternPath)
{
    public string RunId { get; init; } = string.Empty;

    public string RootDirectory { get; init; } = RunDirectory;

    public string RunManifestPath => Path.Combine(RunDirectory, "run.json");

    public string GetPath(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Input => InputPath,
        ArtifactKind.Result => ResultPath,
        ArtifactKind.RunSummary => RunSummaryPath,
        ArtifactKind.EvalReport => EvalReportPath,
        ArtifactKind.Pattern => PatternPath,
        ArtifactKind.RunManifest => RunManifestPath,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public IReadOnlyList<string> RequiredPaths =>
        [InputPath, ResultPath, TracePath, RunSummaryPath, EvalReportPath, PatternPath];
}

public sealed record PhaseResult(
    string PhaseName,
    PhaseKind PhaseKind,
    string Content,
    string Provider,
    string? Model);

/// <summary>
/// The lifecycle of a required run artifact. The runtime plans an artifact
/// before it is produced, marks it written on success, and marks it failed when
/// its write is attempted but does not complete. This makes artifact planning
/// and completion explicit instead of inferring them from placeholder content.
/// </summary>
public enum RunArtifactStatus
{
    Planned,
    Written,
    Failed
}

public sealed record RunArtifactState(string Name, RunArtifactStatus Status);
