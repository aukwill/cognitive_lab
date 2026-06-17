namespace CognitiveRuntime.Core.Contracts;

public static class TraceEventNames
{
    public const string RunStarted = "run.started";
    public const string RunCompleted = "run.completed";
    public const string RunFinalized = "run.finalized";
    public const string RunFailed = "run.failed";
    public const string RunCancelled = "run.cancelled";

    public const string PatternStarted = "pattern.started";
    public const string PatternCompleted = "pattern.completed";

    public const string StageStarted = "stage.started";
    public const string StageCompleted = "stage.completed";

    public const string ModeLoaded = "mode.loaded";
    public const string NodeStarted = "node.started";
    public const string NodeCompleted = "node.completed";
    public const string NodeFailed = "node.failed";
    public const string NodeCancelled = "node.cancelled";
    public const string PhaseStarted = "phase.started";
    public const string PhaseCompleted = "phase.completed";

    public const string ModelCalled = "model.called";
    public const string ModelCompleted = "model.completed";
    public const string ModelFailed = "model.failed";

    public const string CriticStarted = "critic.started";
    public const string CriticCompleted = "critic.completed";
    public const string RevisionStarted = "revision.started";
    public const string RevisionCompleted = "revision.completed";

    public const string ArtifactReserved = "artifact.reserved";
    public const string ArtifactWritten = "artifact.written";

    public const string EvalStarted = "eval.started";
    public const string EvalCompleted = "eval.completed";

    public const string BudgetExceeded = "budget.exceeded";

    public const string ToolPolicyEvaluated = "tool.policy_evaluated";
    public const string ToolCalled = "tool.called";
    public const string ToolCompleted = "tool.completed";

    public static bool IsTerminal(string eventType) =>
        eventType is RunFinalized or RunFailed or RunCancelled;
}
