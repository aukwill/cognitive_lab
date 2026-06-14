namespace DocumentDistiller.Core.Contracts;

public sealed record SourceDocument(
    string Id,
    string RelativePath,
    string Title,
    string Content,
    string Sha256);

public sealed record DocumentChunk(
    string Id,
    string SourceId,
    int Sequence,
    int StartCharacter,
    int EndCharacter,
    string Sha256,
    string Content);

public static class ClaimStances
{
    public const string Corroborated = "corroborated";
    public const string SingleSource = "single-source";
    public const string Contested = "contested";
    public const string Inference = "inference";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(
            [Corroborated, SingleSource, Contested, Inference],
            StringComparer.Ordinal);
}

public sealed record EvidenceClaim(
    string Id,
    string Statement,
    string Stance,
    double Confidence,
    string[] EvidenceIds);

public sealed record Pillar(
    string Id,
    string Name,
    string Thesis,
    string Analysis,
    EvidenceClaim[] Claims);

public sealed record DistillationDraft(
    string Title,
    string Topic,
    string CentralQuestion,
    string ExecutiveSummary,
    Pillar[] Pillars,
    string[] CrossCuttingThemes,
    string[] Tensions,
    string[] Gaps,
    string Conclusion);

public sealed record DistillationCritique(
    string[] Strengths,
    string[] Issues,
    string[] MissingEvidenceIds,
    string[] RevisionGuidance);

public sealed record PromptSet(
    string Analyze,
    string Critic,
    string Revise);

public sealed record AnalysisRequest(
    string RunId,
    PromptSet Prompts,
    IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<DocumentChunk> Chunks);

public sealed record CritiqueRequest(
    string RunId,
    PromptSet Prompts,
    IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<DocumentChunk> Chunks,
    DistillationDraft Draft);

public sealed record RevisionRequest(
    string RunId,
    PromptSet Prompts,
    IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<DocumentChunk> Chunks,
    DistillationDraft Draft,
    DistillationCritique Critique);

public sealed record ModelInvocationMetadata(
    string Provider,
    string Model,
    string? ResponseId,
    int InputTokens,
    int OutputTokens,
    int CachedInputTokens,
    int ReasoningTokens);

public sealed record ModelCompletion<T>(
    T Value,
    ModelInvocationMetadata Metadata);

public sealed record DistillationRunRequest(
    string InputDirectory,
    string OutputRoot,
    string PromptsRoot,
    string Provider,
    int MaxInputCharacters = 120_000,
    int ChunkSizeCharacters = 3_000,
    int ChunkOverlapCharacters = 300);

public enum DistillationRunOutcome
{
    Success,
    EvalFailed,
    RuntimeFailed,
    Cancelled
}

public sealed record DistillationRunResult(
    string RunId,
    string OutputDirectory,
    string ReportPath,
    string HtmlReportPath,
    string TracePath,
    string EvalReportPath,
    DistillationRunOutcome Outcome);

public sealed record RunArtifactPaths(
    string RunDirectory,
    string InputManifestPath,
    string RunManifestPath,
    string SourceRiskPath,
    string EvidencePath,
    string EvidenceMatrixPath,
    string ModelUsagePath,
    string DraftPath,
    string CritiquePath,
    string AnalysisPath,
    string ReportPath,
    string HtmlReportPath,
    string TracePath,
    string RunSummaryPath,
    string EvalReportPath)
{
    public IReadOnlyList<string> RequiredPaths =>
    [
        InputManifestPath,
        RunManifestPath,
        SourceRiskPath,
        EvidencePath,
        EvidenceMatrixPath,
        ModelUsagePath,
        DraftPath,
        CritiquePath,
        AnalysisPath,
        ReportPath,
        HtmlReportPath,
        TracePath,
        RunSummaryPath,
        EvalReportPath
    ];
}

public sealed record TraceEvent(
    DateTimeOffset Timestamp,
    string Type,
    string RunId,
    IReadOnlyDictionary<string, object?> Data);

public sealed record TraceDocument(
    string RunId,
    IReadOnlyList<TraceEvent> Events);

public sealed record EvalCheck(
    string Name,
    bool Passed,
    string Details);

public sealed record EvalReport(IReadOnlyList<EvalCheck> Checks)
{
    public bool Passed => Checks.All(check => check.Passed);
}

public sealed record EvalContext(
    RunArtifactPaths Artifacts,
    IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<DocumentChunk> Chunks,
    DistillationDraft Analysis,
    EvidenceMatrix EvidenceMatrix,
    SourceRiskReport SourceRiskReport,
    string Report,
    IReadOnlyList<TraceEvent> TraceEvents);

public sealed record ResponsesApiOptions(
    string ProviderName,
    string ApiKeyEnvironmentVariable,
    string Endpoint,
    string? ApiKey,
    string Model = "gpt-5.5",
    string ReasoningEffort = "medium");

public sealed record IngestionResult(
    IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<DocumentChunk> Chunks);

public sealed record SourceRiskFinding(
    string Id,
    string SourceId,
    string ChunkId,
    string Category,
    string Severity,
    string Pattern,
    string Excerpt);

public sealed record SourceRiskReport(
    IReadOnlyList<SourceRiskFinding> Findings)
{
    public int HighSeverityCount => Findings.Count(
        finding => string.Equals(
            finding.Severity,
            "high",
            StringComparison.Ordinal));
}

public sealed record ClaimEvidenceDiagnostic(
    string ClaimId,
    string PillarId,
    string Stance,
    double Confidence,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> SourceIds,
    double LexicalGroundingScore);

public sealed record PillarEvidenceDiagnostic(
    string PillarId,
    int ClaimCount,
    int UniqueSourceCount,
    double AverageLexicalGroundingScore);

public sealed record EvidenceMatrix(
    IReadOnlyList<ClaimEvidenceDiagnostic> Claims,
    IReadOnlyList<PillarEvidenceDiagnostic> Pillars,
    int UniqueCitedSourceCount,
    double CorpusSourceCoverage,
    double AverageLexicalGroundingScore);

public sealed record RunManifest(
    int SchemaVersion,
    string RunId,
    DateTimeOffset CreatedAt,
    string Provider,
    string Model,
    string Pattern,
    string CorpusSha256,
    string PromptsSha256,
    int DocumentCount,
    int ChunkCount,
    int MaxInputCharacters,
    int ChunkSizeCharacters,
    int ChunkOverlapCharacters);

public sealed class DistillationRunException : Exception
{
    public DistillationRunException(
        string message,
        string outputDirectory,
        Exception innerException)
        : base(message, innerException)
    {
        OutputDirectory = outputDirectory;
    }

    public string OutputDirectory { get; }
}
