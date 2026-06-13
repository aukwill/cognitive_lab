using System.Text;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime;

public static class ResultComposer
{
    public static string Compose(
        LoadedMode mode,
        IReadOnlyList<PhaseResult> phaseResults)
    {
        var mainResults = phaseResults
            .Where(result => result.PhaseKind == PhaseKind.Main)
            .ToArray();
        var criticResults = phaseResults
            .Where(result => result.PhaseKind == PhaseKind.Critic)
            .ToArray();

        if (mainResults.Length == 0)
        {
            throw new InvalidOperationException("No main phase result was produced.");
        }

        if (criticResults.Length == 0)
        {
            throw new InvalidOperationException("No critic phase result was produced.");
        }

        var title = char.ToUpperInvariant(mode.Manifest.Name[0]) +
            mode.Manifest.Name[1..];
        var builder = new StringBuilder()
            .Append("# ")
            .Append(title)
            .AppendLine(" Result")
            .AppendLine();

        AppendResults(builder, mainResults);
        builder.AppendLine().AppendLine("## Critic Review").AppendLine();
        AppendResults(builder, criticResults);

        return builder.ToString();
    }

    private static void AppendResults(
        StringBuilder builder,
        IReadOnlyList<PhaseResult> results)
    {
        for (var index = 0; index < results.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine().AppendLine("---").AppendLine();
            }

            builder.AppendLine(results[index].Content.Trim());
        }
    }
}
