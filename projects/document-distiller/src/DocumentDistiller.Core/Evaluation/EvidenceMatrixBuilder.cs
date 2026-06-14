using System.Text.RegularExpressions;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Evaluation;

public sealed partial class EvidenceMatrixBuilder : IEvidenceMatrixBuilder
{
    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "about", "after", "also", "because", "before", "between", "could",
            "each", "from", "have", "into", "more", "most", "other", "should",
            "such", "than", "that", "their", "there", "these", "they", "this",
            "those", "through", "using", "what", "when", "where", "which",
            "while", "with", "would"
        };

    public EvidenceMatrix Build(
        DistillationDraft analysis,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var chunkById = chunks.ToDictionary(
            chunk => chunk.Id,
            StringComparer.Ordinal);
        var claims = analysis.Pillars
            .SelectMany(
                pillar => pillar.Claims.Select(
                    claim => BuildClaimDiagnostic(
                        pillar.Id,
                        claim,
                        chunkById)))
            .ToArray();
        var pillars = analysis.Pillars
            .Select(
                pillar =>
                {
                    var pillarClaims = claims
                        .Where(claim => claim.PillarId == pillar.Id)
                        .ToArray();
                    return new PillarEvidenceDiagnostic(
                        pillar.Id,
                        pillarClaims.Length,
                        pillarClaims
                            .SelectMany(claim => claim.SourceIds)
                            .Distinct(StringComparer.Ordinal)
                            .Count(),
                        pillarClaims.Length == 0
                            ? 0
                            : pillarClaims.Average(
                                claim => claim.LexicalGroundingScore));
                })
            .ToArray();
        var uniqueCitedSourceCount = claims
            .SelectMany(claim => claim.SourceIds)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new EvidenceMatrix(
            claims,
            pillars,
            uniqueCitedSourceCount,
            documents.Count == 0
                ? 0
                : (double)uniqueCitedSourceCount / documents.Count,
            claims.Length == 0
                ? 0
                : claims.Average(claim => claim.LexicalGroundingScore));
    }

    private static ClaimEvidenceDiagnostic BuildClaimDiagnostic(
        string pillarId,
        EvidenceClaim claim,
        IReadOnlyDictionary<string, DocumentChunk> chunkById)
    {
        var resolvedChunks = claim.EvidenceIds
            .Where(chunkById.ContainsKey)
            .Select(evidenceId => chunkById[evidenceId])
            .ToArray();
        var claimTerms = Tokenize(claim.Statement);
        var evidenceTerms = resolvedChunks
            .SelectMany(chunk => Tokenize(chunk.Content))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedTerms = claimTerms.Count(evidenceTerms.Contains);
        var groundingScore = claimTerms.Count == 0
            ? 0
            : (double)matchedTerms / claimTerms.Count;

        return new ClaimEvidenceDiagnostic(
            claim.Id,
            pillarId,
            claim.Stance,
            claim.Confidence,
            claim.EvidenceIds,
            resolvedChunks
                .Select(chunk => chunk.SourceId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Math.Round(groundingScore, 4));
    }

    private static IReadOnlySet<string> Tokenize(string content) =>
        WordRegex()
            .Matches(content.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(term => term.Length >= 4 && !StopWords.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
