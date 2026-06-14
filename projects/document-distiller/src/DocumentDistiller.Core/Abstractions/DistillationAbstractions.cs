using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Abstractions;

public interface IDocumentIngestor
{
    Task<IngestionResult> IngestAsync(
        string inputDirectory,
        int maxInputCharacters,
        int chunkSizeCharacters,
        int chunkOverlapCharacters,
        CancellationToken cancellationToken = default);
}

public interface ICorpusDiscoveryProvider
{
    string ProviderName { get; }

    Task<IReadOnlyList<CorpusSearchResult>> SearchAsync(
        CorpusSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<CorpusPage> FetchAsync(
        string url,
        CancellationToken cancellationToken = default);
}

public interface ICorpusFunnel
{
    Task<CorpusFunnelResult> BuildAsync(
        CorpusFunnelRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISourceRiskScanner
{
    SourceRiskReport Scan(IReadOnlyList<DocumentChunk> chunks);
}

public interface IEvidenceMatrixBuilder
{
    EvidenceMatrix Build(
        DistillationDraft analysis,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<DocumentChunk> chunks);
}

public interface IDistillationContractValidator
{
    void ValidateDraft(DistillationDraft draft);

    void ValidateFinal(
        DistillationDraft analysis,
        IReadOnlyList<DocumentChunk> chunks);
}

public interface IPromptLoader
{
    Task<PromptSet> LoadAsync(
        string promptsRoot,
        CancellationToken cancellationToken = default);
}

public interface IDistillationModelClient
{
    string ProviderName { get; }

    string ModelName { get; }

    Task<ModelCompletion<DistillationDraft>> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<ModelCompletion<DistillationCritique>> CritiqueAsync(
        CritiqueRequest request,
        CancellationToken cancellationToken = default);

    Task<ModelCompletion<DistillationDraft>> ReviseAsync(
        RevisionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDistillationModelClientFactory
{
    IDistillationModelClient Resolve(string providerName);
}

public interface IRunArtifactWriter
{
    Task<RunArtifactPaths> PrepareRunAsync(
        string outputRoot,
        string runId,
        CancellationToken cancellationToken = default);

    Task WriteTextAsync(
        RunArtifactPaths artifacts,
        string path,
        string content,
        CancellationToken cancellationToken = default);

    Task WriteJsonAsync<T>(
        RunArtifactPaths artifacts,
        string path,
        T value,
        CancellationToken cancellationToken = default);
}

public interface ITraceSession
{
    IReadOnlyList<TraceEvent> Events { get; }

    Task EmitAsync(
        string eventType,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default);
}

public interface ITraceSessionFactory
{
    Task<ITraceSession> CreateAsync(
        string runId,
        string tracePath,
        CancellationToken cancellationToken = default);
}

public interface IDistillationEvaluator
{
    Task<EvalReport> EvaluateAsync(
        EvalContext context,
        CancellationToken cancellationToken = default);
}
