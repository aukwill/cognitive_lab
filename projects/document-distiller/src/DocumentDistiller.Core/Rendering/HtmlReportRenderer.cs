using System.Net;
using System.Text;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Rendering;

public static class HtmlReportRenderer
{
    public static string Render(
        DistillationDraft analysis,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<DocumentChunk> chunks,
        EvidenceMatrix evidenceMatrix,
        SourceRiskReport sourceRiskReport)
    {
        var chunkById = chunks.ToDictionary(
            chunk => chunk.Id,
            StringComparer.Ordinal);
        var documentById = documents.ToDictionary(
            document => document.Id,
            StringComparer.Ordinal);
        var builder = new StringBuilder(
            """
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Document Distiller</title>
              <style>
                :root { color-scheme: light; --ink:#17212b; --muted:#5d6873; --line:#d8dee4;
                  --paper:#fbfaf7; --panel:#fff; --accent:#275d52; --soft:#e8f1ee;
                  --warn:#8a4b08; --risk:#8f2d2d; }
                * { box-sizing:border-box; }
                body { margin:0; background:var(--paper); color:var(--ink);
                  font:16px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif; }
                main { width:min(1120px,calc(100% - 32px)); margin:0 auto; padding:48px 0 72px; }
                h1,h2,h3 { line-height:1.15; letter-spacing:-.02em; }
                h1 { max-width:850px; font-size:clamp(2.3rem,6vw,4.8rem); margin:.15em 0; }
                h2 { margin-top:2.3em; }
                .eyebrow { color:var(--accent); font-weight:750; letter-spacing:.12em;
                  text-transform:uppercase; font-size:.78rem; }
                .lede { max-width:820px; font-size:1.2rem; color:var(--muted); }
                .metrics { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr));
                  gap:12px; margin:32px 0; }
                .metric,.pillar,.claim,.risk,.source { background:var(--panel);
                  border:1px solid var(--line); border-radius:14px; }
                .metric { padding:18px; }
                .metric strong { display:block; font-size:1.65rem; }
                .metric span { color:var(--muted); font-size:.9rem; }
                .pillar { padding:24px; margin:18px 0; }
                .pillar > p { color:var(--muted); }
                .claim { padding:18px; margin:14px 0; border-left:5px solid var(--accent); }
                .claim-head { display:flex; flex-wrap:wrap; gap:8px; align-items:center; }
                .tag,.evidence { display:inline-block; border-radius:999px; padding:3px 9px;
                  font-size:.78rem; font-weight:700; }
                .tag { background:var(--soft); color:var(--accent); }
                .evidence { background:#f2f4f5; color:#34404b; margin:3px 4px 3px 0; }
                .evidence small { color:var(--muted); font-weight:500; }
                .risk { padding:14px 18px; margin:10px 0; border-left:5px solid var(--risk); }
                .source { padding:14px 18px; margin:10px 0; }
                nav { display:flex; flex-wrap:wrap; gap:10px; margin:28px 0; }
                nav a { color:var(--accent); font-weight:700; }
                code { overflow-wrap:anywhere; }
                footer { margin-top:48px; padding-top:20px; border-top:1px solid var(--line);
                  color:var(--muted); }
              </style>
            </head>
            <body>
            <main>
            """);

        builder
            .Append("<div class=\"eyebrow\">Evidence-first distillation</div><h1>")
            .Append(E(analysis.Title))
            .Append("</h1><p class=\"lede\">")
            .Append(E(analysis.ExecutiveSummary))
            .Append("</p><nav>")
            .Append(ArtifactLink("report.md"))
            .Append(ArtifactLink("evidence.json"))
            .Append(ArtifactLink("evidence_matrix.json"))
            .Append(ArtifactLink("source_risk.json"))
            .Append(ArtifactLink("model_usage.json"))
            .Append(ArtifactLink("trace.json"))
            .Append(ArtifactLink("eval_report.md"))
            .Append("</nav><section class=\"metrics\">")
            .Append(Metric(documents.Count.ToString(), "documents"))
            .Append(Metric(evidenceMatrix.Claims.Count.ToString(), "atomic claims"))
            .Append(Metric(
                evidenceMatrix.CorpusSourceCoverage.ToString("P0"),
                "source coverage"))
            .Append(Metric(
                evidenceMatrix.AverageLexicalGroundingScore.ToString("P0"),
                "grounding signal"))
            .Append(Metric(
                sourceRiskReport.Findings.Count.ToString(),
                "source-risk findings"))
            .Append("</section><h2>Research question</h2><p class=\"lede\">")
            .Append(E(analysis.CentralQuestion))
            .Append("</p><h2>Central pillars</h2>");

        foreach (var pillar in analysis.Pillars)
        {
            builder
                .Append("<article class=\"pillar\"><div class=\"eyebrow\">")
                .Append(E(pillar.Id))
                .Append("</div><h3>")
                .Append(E(pillar.Name))
                .Append("</h3><p><strong>")
                .Append(E(pillar.Thesis))
                .Append("</strong></p><p>")
                .Append(E(pillar.Analysis))
                .Append("</p>");

            foreach (var claim in pillar.Claims)
            {
                builder
                    .Append("<section class=\"claim\"><div class=\"claim-head\"><strong>")
                    .Append(E(claim.Id))
                    .Append("</strong><span class=\"tag\">")
                    .Append(E(claim.Stance))
                    .Append("</span><span class=\"tag\">")
                    .Append(claim.Confidence.ToString("P0"))
                    .Append("</span></div><p>")
                    .Append(E(claim.Statement))
                    .Append("</p><div>");

                foreach (var evidenceId in claim.EvidenceIds)
                {
                    builder.Append("<span class=\"evidence\">").Append(E(evidenceId));
                    if (chunkById.TryGetValue(evidenceId, out var chunk) &&
                        documentById.TryGetValue(chunk.SourceId, out var document))
                    {
                        builder
                            .Append(" <small>")
                            .Append(E(document.Title))
                            .Append(" @ ")
                            .Append(chunk.StartCharacter)
                            .Append('-')
                            .Append(chunk.EndCharacter)
                            .Append("</small>");
                    }

                    builder.Append("</span>");
                }

                builder.Append("</div></section>");
            }

            builder.Append("</article>");
        }

        AppendList(builder, "Cross-cutting themes", analysis.CrossCuttingThemes);
        AppendList(builder, "Tensions and contradictions", analysis.Tensions);
        AppendList(builder, "Evidence gaps", analysis.Gaps);

        builder.Append("<h2>Source integrity</h2>");
        if (sourceRiskReport.Findings.Count == 0)
        {
            builder.Append("<p>No deterministic source-risk patterns were detected.</p>");
        }
        else
        {
            foreach (var finding in sourceRiskReport.Findings)
            {
                builder
                    .Append("<div class=\"risk\"><strong>")
                    .Append(E($"{finding.Severity}: {finding.Category}"))
                    .Append("</strong><p>")
                    .Append(E(finding.Excerpt))
                    .Append("</p><code>")
                    .Append(E(finding.ChunkId))
                    .Append("</code></div>");
            }
        }

        builder.Append("<h2>Sources</h2>");
        foreach (var document in documents)
        {
            builder
                .Append("<div class=\"source\"><strong>")
                .Append(E(document.Id))
                .Append(": ")
                .Append(E(document.Title))
                .Append("</strong><br><code>")
                .Append(E(document.RelativePath))
                .Append("</code></div>");
        }

        builder
            .Append("<h2>Conclusion</h2><p class=\"lede\">")
            .Append(E(analysis.Conclusion))
            .Append("</p><footer>Generated from typed runtime data. No scripts or external assets.")
            .Append("</footer></main></body></html>");

        return builder.ToString();
    }

    private static void AppendList(
        StringBuilder builder,
        string heading,
        IReadOnlyList<string> items)
    {
        builder.Append("<h2>").Append(E(heading)).Append("</h2><ul>");
        foreach (var item in items)
        {
            builder.Append("<li>").Append(E(item)).Append("</li>");
        }

        builder.Append("</ul>");
    }

    private static string Metric(string value, string label) =>
        $"<div class=\"metric\"><strong>{E(value)}</strong><span>{E(label)}</span></div>";

    private static string ArtifactLink(string name) =>
        $"<a href=\"{E(name)}\">{E(name)}</a>";

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
