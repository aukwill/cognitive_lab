namespace CognitiveRuntime.Core.Contracts;

public sealed record RunRequest(
    string ModeName,
    string Input,
    string ModelProvider,
    string OutputRoot,
    bool WriteHtmlView = false,
    string? InputSource = null,
    string? Lens = null);

public sealed record RunResult(
    string RunId,
    string OutputDirectory,
    string ResultPath,
    string TracePath,
    string EvalReportPath,
    bool EvalPassed,
    string? HtmlViewPath = null);

public enum ArtifactKind
{
    Input,
    Result,
    RunSummary,
    EvalReport
}

public sealed record RunArtifactPaths(
    string RunDirectory,
    string InputPath,
    string ResultPath,
    string TracePath,
    string RunSummaryPath,
    string EvalReportPath)
{
    public string GetPath(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Input => InputPath,
        ArtifactKind.Result => ResultPath,
        ArtifactKind.RunSummary => RunSummaryPath,
        ArtifactKind.EvalReport => EvalReportPath,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public IReadOnlyList<string> RequiredPaths =>
        [InputPath, ResultPath, TracePath, RunSummaryPath, EvalReportPath];
}

public sealed record PhaseResult(
    string PhaseName,
    PhaseKind PhaseKind,
    string Content,
    string Provider,
    string? Model);
