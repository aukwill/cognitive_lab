namespace CognitiveRuntime.Core.Contracts;

/// <summary>
/// Fixed, typed execution limits the runtime enforces around a run. These are
/// runtime-owned configuration, not model output: the model cannot read or
/// change them. Defaults are generous enough to preserve current mock behavior;
/// callers inject smaller budgets to bound real runs.
/// </summary>
public sealed record RunBudget(
    int MaxInputCharacters,
    int MaxModelCalls,
    int MaxPhaseOutputCharacters,
    TimeSpan MaxRunDuration)
{
    public static RunBudget Default { get; } = new(
        MaxInputCharacters: 100_000,
        MaxModelCalls: 64,
        MaxPhaseOutputCharacters: 200_000,
        MaxRunDuration: TimeSpan.FromMinutes(10));
}
