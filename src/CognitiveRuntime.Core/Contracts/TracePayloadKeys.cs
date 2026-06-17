namespace CognitiveRuntime.Core.Contracts;

/// <summary>
/// Canonical payload-key names for core trace events. Centralizing these keeps
/// producers (the orchestrator, executor, phase runner) and consumers (evals,
/// the model-call analyzer, static views) from drifting on the magic strings
/// they share. Values are the plain JSON keys written to <c>trace.json</c>; this
/// type is a key contract only and never appears in serialized output.
/// </summary>
public static class TracePayloadKeys
{
    // Run and pattern lifecycle.
    public const string Mode = "mode";
    public const string Provider = "provider";
    public const string OutputDirectory = "outputDirectory";
    public const string Pattern = "pattern";
    public const string Stages = "stages";

    // Mode loading.
    public const string SourceId = "sourceId";
    public const string Name = "name";
    public const string Version = "version";
    public const string PhaseCount = "phaseCount";
    public const string Lens = "lens";

    // Execution nodes and pipeline stages.
    public const string NodeId = "nodeId";
    public const string StageId = "stageId";
    public const string StageIndex = "stageIndex";
    public const string Phase = "phase";
    public const string PhaseKind = "phaseKind";
    public const string Kind = "kind";
    public const string Status = "status";
    public const string Dependencies = "dependencies";
    public const string ContextNodeIds = "contextNodeIds";
    public const string InputNodeId = "inputNodeId";
    public const string OutputLength = "outputLength";

    // Model calls.
    public const string CallId = "callId";
    public const string Attempt = "attempt";
    public const string Model = "model";
    public const string ContentLength = "contentLength";
    public const string PriorPhaseCount = "priorPhaseCount";
    public const string Cancelled = "cancelled";

    // Artifacts.
    public const string RelativePath = "relativePath";

    // The planned artifact set announced on artifact.reserved.
    public const string Artifacts = "artifacts";

    // Evaluation and terminal outcome.
    public const string Passed = "passed";
    public const string CheckCount = "checkCount";
    public const string Outcome = "outcome";
    public const string EvalPassed = "evalPassed";
    public const string FromStatus = "fromStatus";
    public const string LifecycleStatus = "lifecycleStatus";

    // Timing.
    public const string DurationMs = "durationMs";

    // Execution budgets.
    public const string BudgetKind = "budgetKind";
    public const string Limit = "limit";
    public const string Observed = "observed";

    // Sanitized failure information.
    public const string Category = "category";
    public const string ExceptionType = "exceptionType";
    public const string Message = "message";
}
