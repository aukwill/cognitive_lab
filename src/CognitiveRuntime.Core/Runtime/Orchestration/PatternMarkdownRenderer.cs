using System.Text;
namespace CognitiveRuntime.Core.Runtime.Orchestration;

/// <summary>
/// Renders the <c>pattern.md</c> artifact from typed pattern data: the
/// selected pattern, its steps or stages, and how context flowed between
/// them for this run.
/// </summary>
public static class PatternMarkdownRenderer
{
    public static string Render(PatternExecutionResult execution) =>
        execution.Plan.Stages.Count == 0
            ? RenderForNodes(execution)
            : RenderForStages(execution);

    private static string RenderForNodes(PatternExecutionResult execution)
    {
        var nodes = execution.NodeResults;
        var builder = new StringBuilder()
            .AppendLine("# Pattern")
            .AppendLine()
            .AppendLine(
                $"This run used the `{execution.Plan.PatternName}` pattern with " +
                $"{nodes.Count} step{(nodes.Count == 1 ? "" : "s")}.")
            .AppendLine()
            .AppendLine("## Steps")
            .AppendLine();

        var resultsByNodeId = nodes.ToDictionary(
            result => result.Node.Id,
            StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var result = nodes[index];
            var kind = result.Phase.Kind.ToString().ToLowerInvariant();
            var context = result.ContextNodeIds.Count == 0
                ? "no prior phase results"
                : string.Join(
                    ", ",
                    result.ContextNodeIds.Select(
                        nodeId => resultsByNodeId[nodeId].Phase.Name));

            builder.AppendLine(
                $"{index + 1}. `{result.Phase.Name}` ({kind}) - context: {context}");
        }

        return builder.ToString();
    }

    private static string RenderForStages(PatternExecutionResult execution)
    {
        var stages = execution.Stages;
        var builder = new StringBuilder()
            .AppendLine("# Pattern")
            .AppendLine()
            .AppendLine(
                $"This run used the `{execution.Plan.PatternName}` pattern with " +
                $"{stages.Count} stage{(stages.Count == 1 ? "" : "s")}: " +
                $"`{string.Join(" -> ", stages.Select(stage => stage.ModeName))}`.")
            .AppendLine()
            .AppendLine("## Stages")
            .AppendLine();

        for (var index = 0; index < stages.Count; index++)
        {
            var stage = stages[index];
            var input = index == 0
                ? "the run's initial input"
                : $"stage {index:D2} (`{stages[index - 1].ModeName}`)'s authoritative revision";

            builder.AppendLine($"{stage.StageIndex}. `{stage.ModeName}` - input: {input}");
        }

        return builder.ToString();
    }
}
