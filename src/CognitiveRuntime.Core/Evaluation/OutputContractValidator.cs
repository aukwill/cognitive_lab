using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Evaluation;

public sealed class OutputContractValidator
{
    public EvalCheckResult Validate(string content, OutputContract contract)
    {
        var lines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingHeadings = contract.RequiredHeadings
            .Where(heading => !lines.Contains(heading.Trim()))
            .ToArray();

        var lengthSatisfied = content.Trim().Length >= contract.MinimumLength;
        var passed = lengthSatisfied && missingHeadings.Length == 0;

        var details = passed
            ? "All required headings and minimum length were satisfied."
            : BuildFailureDetails(lengthSatisfied, contract.MinimumLength, missingHeadings);

        return new EvalCheckResult("output contract satisfied", passed, details);
    }

    private static string BuildFailureDetails(
        bool lengthSatisfied,
        int minimumLength,
        IReadOnlyList<string> missingHeadings)
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

        return string.Join(" ", reasons);
    }
}
