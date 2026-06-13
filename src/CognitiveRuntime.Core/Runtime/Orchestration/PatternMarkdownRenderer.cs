using System.Text;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// A single pattern step as it was actually executed for this run, including
/// which prior phase results were selected as its context.
/// </summary>
public sealed record PatternStepExecution(
    string Name,
    PhaseKind Kind,
    IReadOnlyList<string> ContextPhaseNames);

/// <summary>
/// Renders the <c>pattern.md</c> artifact from typed pattern data: the
/// selected pattern, its steps or stages, and how context flowed between
/// them for this run.
/// </summary>
public static class PatternMarkdownRenderer
{
    public static string RenderForSteps(
        string patternName,
        IReadOnlyList<PatternStepExecution> steps)
    {
        var builder = new StringBuilder()
            .AppendLine("# Pattern")
            .AppendLine()
            .AppendLine(
                $"This run used the `{patternName}` pattern with {steps.Count} " +
                $"step{(steps.Count == 1 ? "" : "s")}.")
            .AppendLine()
            .AppendLine("## Steps")
            .AppendLine();

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var kind = step.Kind.ToString().ToLowerInvariant();
            var context = step.ContextPhaseNames.Count == 0
                ? "no prior phase results"
                : string.Join(", ", step.ContextPhaseNames);

            builder.AppendLine(
                $"{index + 1}. `{step.Name}` ({kind}) - context: {context}");
        }

        return builder.ToString();
    }

    public static string RenderForPipeline(
        string patternName,
        IReadOnlyList<string> stageModeNames)
    {
        var builder = new StringBuilder()
            .AppendLine("# Pattern")
            .AppendLine()
            .AppendLine(
                $"This run used the `{patternName}` pattern with {stageModeNames.Count} " +
                $"stage{(stageModeNames.Count == 1 ? "" : "s")}: " +
                $"`{string.Join(" -> ", stageModeNames)}`.")
            .AppendLine()
            .AppendLine("## Stages")
            .AppendLine();

        for (var index = 0; index < stageModeNames.Count; index++)
        {
            var stageIndex = index + 1;
            var modeName = stageModeNames[index];
            var input = index == 0
                ? "the run's initial input"
                : $"stage {index:D2} (`{stageModeNames[index - 1]}`)'s authoritative revision";

            builder.AppendLine($"{stageIndex}. `{modeName}` - input: {input}");
        }

        return builder.ToString();
    }
}
