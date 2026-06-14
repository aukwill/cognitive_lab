using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Tracing;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class ModelCallPairingEvaluator
{
    public EvalCheckResult Evaluate(IReadOnlyList<TraceEvent> events)
    {
        var analysis = ModelCallTraceAnalyzer.Analyze(events);
        return new EvalCheckResult(
            "model call pairing",
            analysis.IsValid,
            analysis.IsValid
                ? $"All {analysis.Calls.Count} model call(s) have exactly one " +
                  "correlated completion or failure."
                : string.Join(" ", analysis.Issues));
    }
}
