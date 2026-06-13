namespace CognitiveRuntime.Core.Contracts;

public sealed record EvalCheckResult(string Name, bool Passed, string Details);

public sealed record EvalReport(IReadOnlyList<EvalCheckResult> Checks)
{
    public bool Passed => Checks.All(check => check.Passed);
}

public sealed record EvalContext(
    RunArtifactPaths Artifacts,
    LoadedMode Mode,
    IReadOnlyList<TraceEvent> TraceEvents,
    IReadOnlyList<PhaseResult> PhaseResults);
