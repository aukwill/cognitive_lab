using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Views;

public sealed record RunViewModel(
    RunViewRun Run,
    IReadOnlyList<RunViewArtifact> Artifacts,
    RunViewPattern Pattern,
    RunViewMode Mode,
    IReadOnlyList<RunViewPhase> Phases,
    IReadOnlyList<RunViewToolPolicyDecision> ToolPolicyDecisions,
    RunViewEval Eval,
    RunViewTrace Trace);

public sealed record RunViewRun(
    string RunId,
    string ModeName,
    string InputSource,
    string ModelProvider,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string OutputDirectory);

public sealed record RunViewArtifact(string Name, string RelativePath);

public sealed record RunViewPattern(
    string Name,
    string UnitName,
    IReadOnlyList<RunViewPatternNode> Nodes,
    IReadOnlyList<RunViewPatternEdge> Edges);

public sealed record RunViewPatternNode(
    string Id,
    string Label,
    string Kind,
    string Detail);

public sealed record RunViewPatternEdge(
    string FromNodeId,
    string ToNodeId,
    string Label);

public sealed record RunViewMode(
    string Name,
    string Description,
    string SequenceLabel,
    IReadOnlyList<string> SequenceNames,
    string CompletionRule);

public sealed record RunViewPhase(
    string Name,
    string Status,
    string ModelProvider,
    string Role,
    int ToolCallsRequested,
    int ToolCallsExecuted,
    string OutputSummary);

public sealed record RunViewToolPolicyDecision(
    string ToolName,
    string RequestedAction,
    string Decision,
    string Reason,
    string Phase);

public sealed record RunViewEval(
    bool Passed,
    IReadOnlyList<EvalCheckResult> Checks);

public sealed record RunViewTrace(
    string RelativePath,
    int EventCount);
