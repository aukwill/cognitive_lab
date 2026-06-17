using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Runtime;

/// <summary>
/// Enforces a run's <see cref="RunBudget"/> from outside the model clients. The
/// runtime consults it at phase boundaries; on a breach it emits a
/// <c>budget.exceeded</c> trace event carrying the limit and observed value and
/// throws <see cref="BudgetExceededException"/>, which the orchestrator maps to a
/// terminal budget failure.
/// </summary>
public sealed class RunBudgetEnforcer
{
    private readonly RunBudget _budget;
    private readonly TimeProvider _timeProvider;
    private readonly ITraceSession _trace;
    private readonly DateTimeOffset _startedAt;
    private int _modelCalls;

    public RunBudgetEnforcer(
        RunBudget budget,
        TimeProvider timeProvider,
        ITraceSession trace)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(trace);
        _budget = budget;
        _timeProvider = timeProvider;
        _trace = trace;
        _startedAt = timeProvider.GetUtcNow();
    }

    public Task CheckInputAsync(
        int inputCharacters,
        CancellationToken cancellationToken = default) =>
        inputCharacters > _budget.MaxInputCharacters
            ? FailAsync(
                "inputCharacters",
                _budget.MaxInputCharacters,
                inputCharacters,
                cancellationToken)
            : Task.CompletedTask;

    public async Task BeforeModelCallAsync(
        CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _modelCalls);
        if (count > _budget.MaxModelCalls)
        {
            await FailAsync(
                "modelCalls",
                _budget.MaxModelCalls,
                count,
                cancellationToken);
        }
    }

    public Task CheckPhaseOutputAsync(
        int outputCharacters,
        CancellationToken cancellationToken = default) =>
        outputCharacters > _budget.MaxPhaseOutputCharacters
            ? FailAsync(
                "phaseOutputCharacters",
                _budget.MaxPhaseOutputCharacters,
                outputCharacters,
                cancellationToken)
            : Task.CompletedTask;

    public Task CheckDurationAsync(CancellationToken cancellationToken = default)
    {
        var elapsedMs = RuntimeDuration.GetMilliseconds(
            _startedAt,
            _timeProvider.GetUtcNow());
        var limitMs = (long)_budget.MaxRunDuration.TotalMilliseconds;
        return elapsedMs > limitMs
            ? FailAsync("runDurationMs", limitMs, elapsedMs, cancellationToken)
            : Task.CompletedTask;
    }

    private async Task FailAsync(
        string budgetKind,
        long limit,
        long observed,
        CancellationToken cancellationToken)
    {
        await _trace.EmitAsync(
            TraceEventNames.BudgetExceeded,
            new Dictionary<string, object?>
            {
                [TracePayloadKeys.BudgetKind] = budgetKind,
                [TracePayloadKeys.Limit] = limit,
                [TracePayloadKeys.Observed] = observed
            },
            cancellationToken);
        throw new BudgetExceededException(budgetKind, limit, observed);
    }
}
