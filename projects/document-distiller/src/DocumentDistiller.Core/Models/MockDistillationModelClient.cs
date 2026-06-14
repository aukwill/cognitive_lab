using System.Globalization;
using System.Text.RegularExpressions;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Models;

public sealed partial class MockDistillationModelClient : IDistillationModelClient
{
    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "about", "after", "also", "answer", "because", "becomes", "been", "before", "being",
            "between", "both", "could", "document", "each", "from", "have",
            "into", "more", "most", "other", "should", "some", "such", "than",
            "that", "their", "there", "these", "they", "this", "those", "through",
            "using", "very", "what", "when", "where", "which", "while", "with",
            "without", "would"
        };

    public string ProviderName => "mock";

    public string ModelName => "deterministic-keyword-v3";

    public Task<ModelCompletion<DistillationDraft>> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keywords = FindKeywords(request.Documents);
        var topic = string.Join(
            ", ",
            keywords.Take(3).Select(ToTitleCase));
        var pillarTerms = keywords.Take(Math.Clamp(keywords.Length, 2, 3)).ToArray();
        var pillars = pillarTerms
            .Select(
                (term, index) =>
                {
                    var evidence = request.Chunks
                        .Where(chunk => ContainsTerm(chunk.Content, term))
                        .Select(chunk => chunk.Id)
                        .Take(3)
                        .ToArray();
                    if (evidence.Length == 0)
                    {
                        evidence = request.Chunks
                            .Skip(index % request.Chunks.Count)
                            .Take(2)
                            .Select(chunk => chunk.Id)
                            .ToArray();
                    }

                    var sourceCount = evidence
                        .Select(id => id.Split("-C", StringSplitOptions.None)[0])
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    var anchorChunk = request.Chunks.Single(
                        chunk => chunk.Id == evidence[0]);
                    var claims = new[]
                    {
                        new EvidenceClaim(
                            $"C{index + 1:D2}-01",
                            $"{ToTitleCase(term)} appears across the corpus as a recurring concern.",
                            sourceCount >= 2
                                ? ClaimStances.Corroborated
                                : ClaimStances.SingleSource,
                            sourceCount >= 2 ? 0.86 : 0.68,
                            evidence),
                        new EvidenceClaim(
                            $"C{index + 1:D2}-02",
                            ExtractClaimSentence(anchorChunk.Content, term),
                            ClaimStances.SingleSource,
                            0.74,
                            [anchorChunk.Id])
                    };

                    return new Pillar(
                        $"P{index + 1:D2}",
                        ToTitleCase(term),
                        $"The corpus repeatedly treats {term} as a central organizing idea.",
                        $"Across the supplied documents, {term} connects operating choices, " +
                        "constraints, and expected outcomes. The evidence should be read as a " +
                        "cross-document pattern rather than as an isolated summary.",
                        claims);
                })
            .ToArray();

        var draft = new DistillationDraft(
            $"A Distillation of {topic}",
            topic,
            $"How do the documents collectively frame {topic}?",
            $"The {request.Documents.Count} supplied documents converge on {topic}. " +
            "The report organizes their shared claims into evidence-backed pillars, " +
            "then separates tensions and unresolved questions from the central synthesis.",
            pillars,
            keywords.Skip(1).Take(3).Select(ToTitleCase).ToArray(),
            [
                "The sources emphasize different levels of abstraction, so implementation " +
                "details and governing principles do not always align cleanly."
            ],
            [
                "The corpus does not establish external benchmarks or longitudinal outcomes."
            ],
            "The strongest reading is a systems view: the documents matter most when their " +
            "themes are treated as mutually reinforcing constraints rather than independent tips.");

        return Task.FromResult(Complete(draft));
    }

    public Task<ModelCompletion<DistillationCritique>> CritiqueAsync(
        CritiqueRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var knownEvidence = request.Chunks.Select(chunk => chunk.Id).ToHashSet();
        var missingEvidence = request.Draft.Pillars
            .SelectMany(pillar => pillar.Claims)
            .SelectMany(claim => claim.EvidenceIds)
            .Where(evidenceId => !knownEvidence.Contains(evidenceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(
            Complete(
            new DistillationCritique(
                [
                    "The draft identifies a central question and organizes the corpus into pillars.",
                    "Pillar claims are linked to runtime-owned evidence identifiers."
                ],
                missingEvidence.Length == 0
                    ? ["The conclusion can state the relationship among pillars more explicitly."]
                    : ["One or more evidence references do not resolve to ingested chunks."],
                missingEvidence,
                [
                    "Preserve the evidence graph.",
                    "Make the conclusion explain how the pillars interact."
                ])));
    }

    public Task<ModelCompletion<DistillationDraft>> ReviseAsync(
        RevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revised = request.Draft with
        {
            Conclusion = request.Draft.Conclusion +
                " Read together, the pillars form an operating model: evidence constrains " +
                "interpretation, governance constrains action, and evaluation closes the loop."
        };

        return Task.FromResult(Complete(revised));
    }

    private ModelCompletion<T> Complete<T>(T value) =>
        new(
            value,
            new ModelInvocationMetadata(
                ProviderName,
                ModelName,
                null,
                0,
                0,
                0,
                0));

    private static string[] FindKeywords(IReadOnlyList<SourceDocument> documents)
    {
        var termCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var documentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            var terms = WordRegex()
                .Matches(document.Content.ToLowerInvariant())
                .Select(match => match.Value)
                .Where(term => term.Length >= 4 && !StopWords.Contains(term))
                .ToArray();

            foreach (var term in terms)
            {
                termCounts[term] = termCounts.GetValueOrDefault(term) + 1;
            }

            foreach (var term in terms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                documentCounts[term] = documentCounts.GetValueOrDefault(term) + 1;
            }
        }

        var keywords = termCounts
            .OrderByDescending(
                pair =>
                    documentCounts.GetValueOrDefault(pair.Key) * 5 +
                    Math.Min(pair.Value, 5))
            .ThenByDescending(pair => documentCounts.GetValueOrDefault(pair.Key))
            .ThenByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .Take(6)
            .ToArray();

        return keywords.Length >= 2
            ? keywords
            : ["system", "evidence", "evaluation"];
    }

    private static bool ContainsTerm(string content, string term) =>
        content.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string ExtractClaimSentence(string content, string term)
    {
        var normalized = string.Join(
            " ",
            content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(
                    line =>
                        line.Length > 0 &&
                        !line.StartsWith('#')));
        var sentences = normalized.Split(
            ['.', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        var sentence = sentences.FirstOrDefault(
            candidate => candidate.Contains(
                term,
                StringComparison.OrdinalIgnoreCase))
            ?? sentences.FirstOrDefault()
            ?? $"{ToTitleCase(term)} is present in the corpus";
        return sentence.Length <= 220
            ? sentence
            : sentence[..220].TrimEnd();
    }

    private static string ToTitleCase(string value) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
