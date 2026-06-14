using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Evaluation;

namespace DocumentDistiller.Tests;

public sealed class EvidenceMatrixBuilderTests
{
    [Fact]
    public void Build_ComputesClaimGroundingAndSourceDiversity()
    {
        var documents = new[]
        {
            new SourceDocument("D001", "one.md", "One", "Evidence runtime", "HASH"),
            new SourceDocument("D002", "two.md", "Two", "Evidence evaluation", "HASH")
        };
        var chunks = new[]
        {
            new DocumentChunk(
                "D001-C001", "D001", 1, 0, 16, "HASH", "Evidence runtime"),
            new DocumentChunk(
                "D002-C001", "D002", 1, 0, 19, "HASH", "Evidence evaluation")
        };
        var analysis = new DistillationDraft(
            "Title",
            "Topic",
            "Question?",
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
                            "Evidence connects runtime evaluation.",
                            ClaimStances.Corroborated,
                            0.85,
                            ["D001-C001", "D002-C001"])
                    ]),
                new Pillar(
                    "P02",
                    "Runtime",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C02",
                            "Runtime evidence is explicit.",
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

        Assert.Equal(2, matrix.UniqueCitedSourceCount);
        Assert.Equal(1, matrix.CorpusSourceCoverage);
        Assert.All(
            matrix.Claims,
            claim => Assert.True(claim.LexicalGroundingScore > 0));
        Assert.Equal(2, matrix.Claims[0].SourceIds.Count);
    }
}
