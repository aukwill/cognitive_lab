using System.Text.RegularExpressions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

/// <summary>
/// A single critic finding parsed from the critic's required
/// <c>## Findings</c> section. Each finding references the required output
/// heading it is concerned with.
/// </summary>
public sealed record CriticFinding(string SectionHeading, string Description);

/// <summary>
/// Runtime-owned thresholds for the loop-efficacy check. These are never
/// derived from model output.
/// </summary>
public sealed record LoopEfficacyOptions
{
    public static LoopEfficacyOptions Default { get; } = new();

    /// <summary>
    /// The minimum number of required output sections that must differ
    /// between the initial draft and the authoritative revision.
    /// </summary>
    public int MinimumChangedSections { get; init; } = 1;
}

/// <summary>
/// Deterministically checks whether the revision phase materially responded
/// to the critic. No model is used and no semantic quality judgment is made.
/// </summary>
public sealed partial class LoopEfficacyEvaluator
{
    private const string FindingsHeading = "Findings";

    private readonly LoopEfficacyOptions _options;

    public LoopEfficacyEvaluator(LoopEfficacyOptions? options = null)
    {
        _options = options ?? LoopEfficacyOptions.Default;
    }

    public EvalCheckResult Evaluate(
        string draft,
        string criticContent,
        string revision,
        OutputContract contract)
    {
        var changedHeadings = DiffRequiredSections(draft, revision, contract.RequiredHeadings);

        if (changedHeadings.Count < _options.MinimumChangedSections)
        {
            var insufficientChangeDetails = changedHeadings.Count == 0
                ? "The revision did not change any required section from the initial draft."
                : $"The revision changed {changedHeadings.Count} required section(s), " +
                  $"below the minimum of {_options.MinimumChangedSections}.";

            return new EvalCheckResult("loop responded to critic findings", false, insufficientChangeDetails);
        }

        var findings = ParseFindings(criticContent);
        if (findings.Count == 0)
        {
            return new EvalCheckResult(
                "loop responded to critic findings",
                false,
                $"The critic produced no parseable findings under '## {FindingsHeading}'.");
        }

        var findingStatus = findings
            .Select(finding => new
            {
                finding,
                heading = MatchRequiredHeading(contract.RequiredHeadings, finding.SectionHeading)
            })
            .Select(match => new
            {
                match.finding,
                match.heading,
                changed = match.heading is not null && changedHeadings.Contains(match.heading)
            })
            .ToList();

        var flaggedChangedCount = findingStatus.Count(status => status.changed);
        var summary = string.Join(
            "; ",
            findingStatus.Select(status => status.heading is null
                ? $"[{status.finding.SectionHeading}] does not match a required heading"
                : $"[{status.heading}] {(status.changed ? "changed" : "unchanged")}"));

        var passed = flaggedChangedCount > 0;
        var details = passed
            ? $"The revision changed {flaggedChangedCount} of {findingStatus.Count} flagged section(s). {summary}."
            : $"No flagged section changed in the revision. {summary}.";

        return new EvalCheckResult("loop responded to critic findings", passed, details);
    }

    /// <summary>
    /// Parses critic findings declared as Markdown list items under a
    /// required <c>## Findings</c> heading, in the form
    /// <c>- [Heading Text] description</c>.
    /// </summary>
    public static IReadOnlyList<CriticFinding> ParseFindings(string criticContent)
    {
        var headings = MarkdownHeadingParser.Parse(criticContent);
        var findingsHeading = headings.FirstOrDefault(heading =>
            string.Equals(heading.Text, FindingsHeading, StringComparison.OrdinalIgnoreCase));

        if (findingsHeading is null)
        {
            return [];
        }

        var section = MarkdownHeadingParser.ExtractSectionContent(criticContent, findingsHeading, headings);
        var findings = new List<CriticFinding>();

        foreach (var line in section.Split('\n'))
        {
            var match = FindingLinePattern().Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            findings.Add(new CriticFinding(
                match.Groups["heading"].Value.Trim(),
                match.Groups["description"].Value.Trim()));
        }

        return findings;
    }

    private static HashSet<string> DiffRequiredSections(
        string draft,
        string revision,
        IReadOnlyList<string> requiredHeadings)
    {
        var draftHeadings = MarkdownHeadingParser.Parse(draft);
        var revisionHeadings = MarkdownHeadingParser.Parse(revision);
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var required in requiredHeadings)
        {
            if (!MarkdownHeadingParser.TryParseHeading(required.Trim(), out var level, out var text))
            {
                continue;
            }

            var draftSection = ExtractSection(draft, draftHeadings, level, text);
            var revisionSection = ExtractSection(revision, revisionHeadings, level, text);

            if (!string.Equals(draftSection, revisionSection, StringComparison.Ordinal))
            {
                changed.Add(required.Trim());
            }
        }

        return changed;
    }

    private static string? ExtractSection(
        string content,
        IReadOnlyList<MarkdownHeading> headings,
        int level,
        string text)
    {
        var heading = headings.FirstOrDefault(candidate =>
            candidate.Level == level &&
            string.Equals(candidate.Text, text, StringComparison.OrdinalIgnoreCase));

        return heading is null
            ? null
            : MarkdownHeadingParser.ExtractSectionContent(content, heading, headings);
    }

    private static string? MatchRequiredHeading(IReadOnlyList<string> requiredHeadings, string sectionName)
    {
        var normalized = sectionName.Trim().TrimStart('#').Trim();

        return requiredHeadings.FirstOrDefault(required =>
            MarkdownHeadingParser.TryParseHeading(required.Trim(), out _, out var text) &&
            string.Equals(text, normalized, StringComparison.OrdinalIgnoreCase))
            ?.Trim();
    }

    [GeneratedRegex(@"^[-*]\s*\[(?<heading>[^\]]+)\]\s*(?<description>.*)$")]
    private static partial Regex FindingLinePattern();
}
