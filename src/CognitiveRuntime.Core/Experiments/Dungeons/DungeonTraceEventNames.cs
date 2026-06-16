namespace CognitiveRuntime.Core.Experiments.Dungeons;

// Experiment-scoped trace event names. These are deliberately separate from
// the core TraceEventNames contract, which is a closed set of runtime
// lifecycle events that evals, lifecycle projection, and static views depend
// on (enforced by TraceEventNamesTests). Centralizing them here removes bare
// string literals from the runner without widening the core contract.
internal static class DungeonTraceEventNames
{
    public const string SelectionCompleted = "selection.completed";
    public const string CandidateStarted = "candidate.started";
    public const string CandidateCompleted = "candidate.completed";
    public const string VerifierCompleted = "verifier.completed";
}
