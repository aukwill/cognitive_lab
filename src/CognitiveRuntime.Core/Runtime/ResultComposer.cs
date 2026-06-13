using System.Text;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Runtime;

public static class ResultComposer
{
    public static string Compose(
        LoadedMode mode,
        IReadOnlyList<PhaseResult> phaseResults)
    {
        var mainResult = GetSingleResult(phaseResults, PhaseKind.Main);
        var criticResult = GetSingleResult(phaseResults, PhaseKind.Critic);
        var revisionResult = GetSingleResult(phaseResults, PhaseKind.Revision);

        var title = char.ToUpperInvariant(mode.Manifest.Name[0]) +
            mode.Manifest.Name[1..];
        var builder = new StringBuilder()
            .Append("# ")
            .Append(title)
            .AppendLine(" Result")
            .AppendLine();

        builder
            .AppendLine("## Authoritative Revision")
            .AppendLine()
            .AppendLine(revisionResult.Content.Trim())
            .AppendLine()
            .AppendLine("## Initial Draft")
            .AppendLine()
            .AppendLine(mainResult.Content.Trim())
            .AppendLine()
            .AppendLine("## Critic Review")
            .AppendLine()
            .AppendLine(criticResult.Content.Trim());

        return builder.ToString();
    }

    private static PhaseResult GetSingleResult(
        IReadOnlyList<PhaseResult> phaseResults,
        PhaseKind phaseKind)
    {
        var matches = phaseResults
            .Where(result => result.PhaseKind == phaseKind)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {phaseKind.ToString().ToLowerInvariant()} " +
                $"phase result, but found {matches.Length}.");
        }

        return matches[0];
    }
}
