using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Evaluation;

public sealed class DistillationContractValidator : IDistillationContractValidator
{
    public void ValidateDraft(DistillationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireText(draft.Title, "title");
        RequireText(draft.Topic, "topic");
        RequireText(draft.CentralQuestion, "central question");
        RequireText(draft.ExecutiveSummary, "executive summary");
        RequireText(draft.Conclusion, "conclusion");

        if (draft.Pillars.Length is < 2 or > 5)
        {
            throw new InvalidOperationException(
                $"A distillation must contain two to five pillars; found " +
                $"{draft.Pillars.Length}.");
        }

        var pillarIds = new HashSet<string>(StringComparer.Ordinal);
        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pillar in draft.Pillars)
        {
            RequireText(pillar.Id, "pillar ID");
            RequireText(pillar.Name, $"pillar '{pillar.Id}' name");
            RequireText(pillar.Thesis, $"pillar '{pillar.Id}' thesis");
            RequireText(pillar.Analysis, $"pillar '{pillar.Id}' analysis");
            if (!pillarIds.Add(pillar.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate pillar ID '{pillar.Id}'.");
            }

            if (pillar.Claims.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Pillar '{pillar.Id}' must contain at least one atomic claim.");
            }

            foreach (var claim in pillar.Claims)
            {
                RequireText(claim.Id, "claim ID");
                RequireText(claim.Statement, $"claim '{claim.Id}' statement");
                if (!claimIds.Add(claim.Id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate claim ID '{claim.Id}'.");
                }

                if (!ClaimStances.All.Contains(claim.Stance))
                {
                    throw new InvalidOperationException(
                        $"Claim '{claim.Id}' has unsupported stance " +
                        $"'{claim.Stance}'.");
                }

                if (claim.Confidence is < 0 or > 1)
                {
                    throw new InvalidOperationException(
                        $"Claim '{claim.Id}' confidence must be between 0 and 1.");
                }

                if (claim.EvidenceIds.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Claim '{claim.Id}' must cite at least one evidence chunk.");
                }
            }
        }
    }

    public void ValidateFinal(
        DistillationDraft analysis,
        IReadOnlyList<DocumentChunk> chunks)
    {
        ValidateDraft(analysis);
        var evidenceIds = chunks
            .Select(chunk => chunk.Id)
            .ToHashSet(StringComparer.Ordinal);
        var unknown = analysis.Pillars
            .SelectMany(pillar => pillar.Claims)
            .SelectMany(claim => claim.EvidenceIds)
            .Where(evidenceId => !evidenceIds.Contains(evidenceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"The final analysis cites unknown evidence IDs: " +
                $"{string.Join(", ", unknown)}.");
        }
    }

    private static void RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"The model returned an empty {fieldName}.");
        }
    }
}
