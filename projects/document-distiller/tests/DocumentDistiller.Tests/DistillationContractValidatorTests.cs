using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Evaluation;

namespace DocumentDistiller.Tests;

public sealed class DistillationContractValidatorTests
{
    [Fact]
    public void ValidateFinal_RejectsUnknownEvidenceBeforeRendering()
    {
        var analysis = CreateAnalysis("D999-C999");
        var chunks = new[]
        {
            new DocumentChunk(
                "D001-C001",
                "D001",
                1,
                0,
                8,
                "HASH",
                "Evidence")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DistillationContractValidator().ValidateFinal(
                analysis,
                chunks));

        Assert.Contains("D999-C999", exception.Message);
    }

    [Fact]
    public void ValidateDraft_RejectsUnsupportedClaimStance()
    {
        var analysis = CreateAnalysis("D001-C001");
        analysis.Pillars[0].Claims[0] = analysis.Pillars[0].Claims[0] with
        {
            Stance = "certain"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DistillationContractValidator().ValidateDraft(analysis));

        Assert.Contains("unsupported stance", exception.Message);
    }

    private static DistillationDraft CreateAnalysis(string evidenceId) =>
        new(
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
                            "Evidence supports the claim.",
                            ClaimStances.SingleSource,
                            0.8,
                            [evidenceId])
                    ]),
                new Pillar(
                    "P02",
                    "Evaluation",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C02",
                            "Evaluation closes the loop.",
                            ClaimStances.SingleSource,
                            0.7,
                            ["D001-C001"])
                    ])
            ],
            ["Theme"],
            ["Tension"],
            ["Gap"],
            "Conclusion");
}
