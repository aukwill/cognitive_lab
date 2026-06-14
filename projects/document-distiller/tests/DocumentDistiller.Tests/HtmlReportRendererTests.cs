using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Evaluation;
using DocumentDistiller.Core.Rendering;

namespace DocumentDistiller.Tests;

public sealed class HtmlReportRendererTests
{
    [Fact]
    public void Render_EscapesUntrustedContentAndLinksArtifacts()
    {
        var documents = new[]
        {
            new SourceDocument(
                "D001",
                "source.md",
                "<script>alert('title')</script>",
                "Evidence content",
                "HASH")
        };
        var chunks = new[]
        {
            new DocumentChunk(
                "D001-C001",
                "D001",
                1,
                0,
                16,
                "HASH",
                "Evidence content")
        };
        var analysis = new DistillationDraft(
            "<script>alert('report')</script>",
            "Evidence",
            "What is supported?",
            "Summary",
            [
                new Pillar(
                    "P01",
                    "Evidence",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C01",
                            "<img src=x onerror=alert(1)>",
                            ClaimStances.SingleSource,
                            0.8,
                            ["D001-C001"])
                    ]),
                new Pillar(
                    "P02",
                    "Runtime",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C02",
                            "Evidence content",
                            ClaimStances.SingleSource,
                            0.7,
                            ["D001-C001"])
                    ])
            ],
            ["Theme"],
            ["Tension"],
            ["Gap"],
            "Conclusion");
        var matrix = new EvidenceMatrixBuilder().Build(
            analysis,
            documents,
            chunks);

        var html = HtmlReportRenderer.Render(
            analysis,
            documents,
            chunks,
            matrix,
            new SourceRiskReport([]));

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;img", html);
        Assert.Contains("href=\"evidence_matrix.json\"", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }
}
