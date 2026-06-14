using System.Text;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Rendering;

public static class MarkdownReportRenderer
{
    public static string Render(
        DistillationDraft analysis,
        IReadOnlyList<SourceDocument> documents,
        EvidenceMatrix evidenceMatrix,
        SourceRiskReport sourceRiskReport)
    {
        var builder = new StringBuilder()
            .Append("# ")
            .AppendLine(analysis.Title)
            .AppendLine()
            .AppendLine("## Research Question")
            .AppendLine()
            .AppendLine(analysis.CentralQuestion)
            .AppendLine()
            .AppendLine("## Executive Summary")
            .AppendLine()
            .AppendLine(analysis.ExecutiveSummary)
            .AppendLine()
            .AppendLine("## Central Pillars")
            .AppendLine();

        for (var index = 0; index < analysis.Pillars.Length; index++)
        {
            var pillar = analysis.Pillars[index];
            builder
                .Append("### ")
                .Append(index + 1)
                .Append(". ")
                .AppendLine(pillar.Name)
                .AppendLine()
                .Append("**Thesis:** ")
                .AppendLine(pillar.Thesis)
                .AppendLine()
                .AppendLine(pillar.Analysis)
                .AppendLine();

            foreach (var claim in pillar.Claims)
            {
                builder
                    .Append("#### Claim ")
                    .AppendLine(claim.Id)
                    .AppendLine()
                    .AppendLine(claim.Statement)
                    .AppendLine()
                    .Append("- Stance: `")
                    .Append(claim.Stance)
                    .AppendLine("`")
                    .Append("- Confidence: ")
                    .AppendLine(claim.Confidence.ToString("P0"))
                    .Append("- Evidence: ")
                    .AppendLine(
                        string.Join(
                            " ",
                            claim.EvidenceIds.Select(id => $"[{id}]")))
                    .AppendLine();
            }
        }

        AppendList(builder, "Cross-Cutting Themes", analysis.CrossCuttingThemes);
        AppendList(builder, "Tensions and Contradictions", analysis.Tensions);
        AppendList(builder, "Evidence Gaps", analysis.Gaps);

        builder
            .AppendLine("## Evidence Quality")
            .AppendLine()
            .Append("- Atomic claims: ")
            .AppendLine(evidenceMatrix.Claims.Count.ToString())
            .Append("- Corpus source coverage: ")
            .AppendLine(evidenceMatrix.CorpusSourceCoverage.ToString("P0"))
            .Append("- Mean lexical grounding signal: ")
            .AppendLine(evidenceMatrix.AverageLexicalGroundingScore.ToString("P0"))
            .Append("- Source-risk findings: ")
            .AppendLine(sourceRiskReport.Findings.Count.ToString())
            .AppendLine()
            .AppendLine(
                "Lexical grounding is a deterministic diagnostic, not an entailment score.")
            .AppendLine();

        builder
            .AppendLine("## Conclusion")
            .AppendLine()
            .AppendLine(analysis.Conclusion)
            .AppendLine()
            .AppendLine("## Sources")
            .AppendLine();

        foreach (var document in documents)
        {
            builder
                .Append("- **")
                .Append(document.Id)
                .Append("**: `")
                .Append(document.RelativePath)
                .Append("` - ")
                .AppendLine(document.Title);
        }

        builder
            .AppendLine()
            .AppendLine(
                "Evidence identifiers resolve to exact corpus spans in `evidence.json`. " +
                "Claim-level diagnostics are in `evidence_matrix.json`; source integrity " +
                "findings are in `source_risk.json`.");

        return builder.ToString();
    }

    private static void AppendList(
        StringBuilder builder,
        string heading,
        IReadOnlyList<string> items)
    {
        builder
            .Append("## ")
            .AppendLine(heading)
            .AppendLine();

        if (items.Count == 0)
        {
            builder.AppendLine("- None identified.");
        }
        else
        {
            foreach (var item in items)
            {
                builder.Append("- ").AppendLine(item);
            }
        }

        builder.AppendLine();
    }
}
