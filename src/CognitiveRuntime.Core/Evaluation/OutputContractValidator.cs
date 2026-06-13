using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class OutputContractValidator
{
    public EvalCheckResult Validate(string content, OutputContract contract)
    {
        var headings = MarkdownHeadingParser.Parse(content);
        var requiredHeadings = contract.RequiredHeadings
            .Select(ParseRequiredHeading)
            .ToList();

        var missingHeadings = new List<string>();
        var duplicateHeadings = new List<string>();
        var emptySections = new List<string>();
        var matchedLineNumbers = new List<int>();

        foreach (var required in requiredHeadings)
        {
            var matches = headings
                .Where(heading =>
                    heading.Level == required.Level &&
                    string.Equals(heading.Text, required.Text, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                missingHeadings.Add(required.Raw);
                continue;
            }

            if (matches.Count > 1)
            {
                duplicateHeadings.Add(required.Raw);
            }

            var section = MarkdownHeadingParser.ExtractSectionContent(content, matches[0], headings);
            if (section.Length == 0)
            {
                emptySections.Add(required.Raw);
            }

            matchedLineNumbers.Add(matches[0].LineNumber);
        }

        var orderViolated = !IsNonDecreasing(matchedLineNumbers);
        var lengthSatisfied = content.Trim().Length >= contract.MinimumLength;
        var passed = lengthSatisfied
            && missingHeadings.Count == 0
            && duplicateHeadings.Count == 0
            && emptySections.Count == 0
            && !orderViolated;

        var details = passed
            ? "All required headings and minimum length were satisfied."
            : BuildFailureDetails(
                lengthSatisfied,
                contract.MinimumLength,
                missingHeadings,
                duplicateHeadings,
                emptySections,
                orderViolated);

        return new EvalCheckResult("output contract satisfied", passed, details);
    }

    private static (int Level, string Text, string Raw) ParseRequiredHeading(string heading)
    {
        var raw = heading.Trim();
        if (!MarkdownHeadingParser.TryParseHeading(raw, out var level, out var text))
        {
            throw new InvalidOperationException(
                $"Required heading '{heading}' is not a valid Markdown heading.");
        }

        return (level, text, raw);
    }

    private static bool IsNonDecreasing(IReadOnlyList<int> values)
    {
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] < values[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildFailureDetails(
        bool lengthSatisfied,
        int minimumLength,
        IReadOnlyList<string> missingHeadings,
        IReadOnlyList<string> duplicateHeadings,
        IReadOnlyList<string> emptySections,
        bool orderViolated)
    {
        var reasons = new List<string>();
        if (!lengthSatisfied)
        {
            reasons.Add($"Result is shorter than {minimumLength} characters.");
        }

        if (missingHeadings.Count > 0)
        {
            reasons.Add($"Missing headings: {string.Join(", ", missingHeadings)}.");
        }

        if (duplicateHeadings.Count > 0)
        {
            reasons.Add($"Duplicate required headings: {string.Join(", ", duplicateHeadings)}.");
        }

        if (emptySections.Count > 0)
        {
            reasons.Add($"Empty required sections: {string.Join(", ", emptySections)}.");
        }

        if (orderViolated)
        {
            reasons.Add("Required headings appear out of order.");
        }

        return string.Join(" ", reasons);
    }
}
