namespace CognitiveRuntime.Core.Contracts;

public enum RuntimeFailureCategory
{
    Provider,
    Mode,
    Evaluation,
    Artifact,
    Persistence,
    Cancellation,
    Runtime
}

public sealed record RuntimeFailureInfo(
    RuntimeFailureCategory Category,
    string? Phase,
    string? Provider,
    string ExceptionType,
    string SafeMessage);
