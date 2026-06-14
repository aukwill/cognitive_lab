using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class TerminalTraceIntegrityEvaluator
{
    public EvalCheckResult Evaluate(IReadOnlyList<TraceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var terminalEvents = events
            .Select((traceEvent, index) => (TraceEvent: traceEvent, Index: index))
            .Where(item => TraceEventNames.IsTerminal(item.TraceEvent.Type))
            .ToArray();
        var issues = new List<string>();

        if (terminalEvents.Length == 0)
        {
            issues.Add("Trace has no terminal event.");
        }
        else
        {
            if (terminalEvents.Length != 1)
            {
                issues.Add(
                    $"Trace has {terminalEvents.Length} terminal events; expected 1.");
            }

            var firstTerminal = terminalEvents[0];
            var trailingEventCount = events.Count - firstTerminal.Index - 1;
            if (trailingEventCount > 0)
            {
                issues.Add(
                    $"Trace has {trailingEventCount} event(s) after terminal event " +
                    $"'{firstTerminal.TraceEvent.Type}' at sequence " +
                    $"'{firstTerminal.TraceEvent.Sequence}'.");
            }

            if (firstTerminal.TraceEvent.Type is
                    TraceEventNames.RunFailed or
                    TraceEventNames.RunCancelled &&
                terminalEvents.Skip(1).Any(
                    item => item.TraceEvent.Type == TraceEventNames.RunFinalized))
            {
                issues.Add(
                    $"Trace contains a later success event after " +
                    $"'{firstTerminal.TraceEvent.Type}'.");
            }
        }

        return new EvalCheckResult(
            "terminal trace integrity",
            issues.Count == 0,
            issues.Count == 0
                ? $"Trace ends with exactly one terminal event " +
                  $"'{terminalEvents[0].TraceEvent.Type}'."
                : string.Join(" ", issues));
    }
}
