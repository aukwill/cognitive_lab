using System.Text.RegularExpressions;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Safety;

public sealed partial class SourceRiskScanner : ISourceRiskScanner
{
    private static readonly RiskPattern[] Patterns =
    [
        new(
            "instruction-override",
            "high",
            "ignore previous instructions",
            IgnorePreviousRegex()),
        new(
            "instruction-override",
            "high",
            "system prompt",
            SystemPromptRegex()),
        new(
            "data-exfiltration",
            "high",
            "send or upload secrets",
            ExfiltrationRegex()),
        new(
            "citation-manipulation",
            "medium",
            "do not cite",
            DoNotCiteRegex()),
        new(
            "role-impersonation",
            "medium",
            "you are now",
            RoleImpersonationRegex())
    ];

    public SourceRiskReport Scan(IReadOnlyList<DocumentChunk> chunks)
    {
        var findings = new List<SourceRiskFinding>();

        foreach (var chunk in chunks)
        {
            foreach (var pattern in Patterns)
            {
                var match = pattern.Regex.Match(chunk.Content);
                if (!match.Success)
                {
                    continue;
                }

                findings.Add(
                    new SourceRiskFinding(
                        $"R{findings.Count + 1:D3}",
                        chunk.SourceId,
                        chunk.Id,
                        pattern.Category,
                        pattern.Severity,
                        pattern.Label,
                        CreateExcerpt(chunk.Content, match.Index)));
            }
        }

        return new SourceRiskReport(findings);
    }

    private static string CreateExcerpt(string content, int matchIndex)
    {
        const int radius = 80;
        var start = Math.Max(0, matchIndex - radius);
        var length = Math.Min(content.Length - start, radius * 2);
        return content
            .Substring(start, length)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private sealed record RiskPattern(
        string Category,
        string Severity,
        string Label,
        Regex Regex);

    [GeneratedRegex(
        @"ignore\s+(all\s+|any\s+)?previous\s+(instructions?|prompts?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex IgnorePreviousRegex();

    [GeneratedRegex(
        @"\b(system\s+prompt|developer\s+message)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SystemPromptRegex();

    [GeneratedRegex(
        @"\b(send|upload|post|exfiltrate)\b.{0,40}\b(secret|token|credential|api\s*key)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExfiltrationRegex();

    [GeneratedRegex(
        @"\b(do\s+not|don'?t|omit)\s+(cite|citation|source|evidence)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DoNotCiteRegex();

    [GeneratedRegex(
        @"\byou\s+are\s+now\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex RoleImpersonationRegex();
}
