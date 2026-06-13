using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Cli;

/// <summary>
/// Centralizes the CLI exit-code mapping so success, evaluation failure,
/// runtime failure, cancellation, and usage errors each have one stable code.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int RuntimeFailed = 1;
    public const int UsageError = 2;
    public const int EvalFailed = 3;
    public const int Cancelled = 130;

    public static int FromOutcome(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Success => Success,
        RunOutcome.EvalFailed => EvalFailed,
        RunOutcome.RuntimeFailed => RuntimeFailed,
        RunOutcome.Cancelled => Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}
