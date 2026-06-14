namespace CognitiveRuntime.Core.Runtime;

internal static class RuntimeDuration
{
    public static long GetMilliseconds(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt) =>
        (long)Math.Max(0, (endedAt - startedAt).TotalMilliseconds);
}
