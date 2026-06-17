using System.Text.RegularExpressions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Persistence;

namespace CognitiveRuntime.Core.Runtime;

internal static partial class RuntimeFailureFactory
{
    private const int MaximumSafeMessageLength = 256;

    public static RuntimeFailureInfo Create(
        Exception exception,
        RuntimeFailureCategory category,
        string? phase = null,
        string? provider = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new RuntimeFailureInfo(
            category,
            phase,
            provider,
            exception.GetType().Name,
            SanitizeMessage(exception.Message));
    }

    public static RuntimeFailureInfo CreateForRun(
        Exception exception,
        RunState state)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(state);

        var node = state.ExecutionNodes.FirstOrDefault(
            candidate => candidate.Status is
                Orchestration.ExecutionNodeStatus.Failed or
                Orchestration.ExecutionNodeStatus.Running);
        return Create(
            exception,
            Classify(
                exception,
                state.LastTransition?.From ?? state.LifecycleStatus),
            node?.PhaseName,
            node?.Provider);
    }

    public static Dictionary<string, object?> ToTraceData(
        RuntimeFailureInfo failure) =>
        new()
        {
            [TracePayloadKeys.Category] = ToTraceValue(failure.Category),
            [TracePayloadKeys.Phase] = failure.Phase,
            [TracePayloadKeys.Provider] = failure.Provider,
            [TracePayloadKeys.ExceptionType] = failure.ExceptionType,
            [TracePayloadKeys.Message] = failure.SafeMessage
        };

    private static RuntimeFailureCategory Classify(
        Exception exception,
        RunLifecycleStatus lifecycleStatus) =>
        exception switch
        {
            ModelProviderException => RuntimeFailureCategory.Provider,
            ModeLoadException => RuntimeFailureCategory.Mode,
            RunStateStoreException => RuntimeFailureCategory.Persistence,
            IOException or UnauthorizedAccessException =>
                RuntimeFailureCategory.Artifact,
            _ when lifecycleStatus == RunLifecycleStatus.Evaluating =>
                RuntimeFailureCategory.Evaluation,
            _ => RuntimeFailureCategory.Runtime
        };

    private static string SanitizeMessage(string message)
    {
        var normalized = ControlCharacters()
            .Replace(message.ReplaceLineEndings(" "), " ")
            .Trim();
        normalized = AuthorizationSecret().Replace(
            normalized,
            "$1: [REDACTED]");
        normalized = NamedSecret().Replace(
            normalized,
            "$1=[REDACTED]");
        normalized = QuerySecret().Replace(
            normalized,
            "$1[REDACTED]");

        return normalized.Length <= MaximumSafeMessageLength
            ? normalized
            : string.Concat(
                normalized.AsSpan(0, MaximumSafeMessageLength - 3),
                "...");
    }

    private static string ToTraceValue(RuntimeFailureCategory category)
    {
        var text = category.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    [GeneratedRegex(@"[\u0000-\u001f\u007f]+")]
    private static partial Regex ControlCharacters();

    [GeneratedRegex(
        @"(?i)\b(authorization|proxy-authorization)\s*[:=]\s*(?:bearer\s+)?[^\s,;]+")]
    private static partial Regex AuthorizationSecret();

    [GeneratedRegex(
        @"(?i)[""']?\b(api[-_ ]?key|token|access[-_ ]?token|secret|password|pwd)\b[""']?\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)")]
    private static partial Regex NamedSecret();

    [GeneratedRegex(
        @"(?i)([?&](?:api[-_]?key|token|access_token)=)[^&\s]+")]
    private static partial Regex QuerySecret();
}
