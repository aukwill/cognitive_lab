using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Persistence;
using CognitiveRuntime.Core.Views;

namespace CognitiveRuntime.Core.Abstractions;

public interface IModeLoader
{
    Task<LoadedMode> LoadAsync(
        string modeName,
        string? lens = null,
        CancellationToken cancellationToken = default);
}

public interface IModelClient
{
    string ProviderName { get; }

    Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default);
}

public interface IModelClientFactory
{
    IModelClient Resolve(string providerName);
}

public interface IRunIdGenerator
{
    string GenerateRunId();
}

public interface IToolProvider
{
    string ProviderName { get; }

    Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        CancellationToken cancellationToken = default);
}

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(
        IToolProvider provider,
        ToolRequest request,
        ITraceSession trace,
        CancellationToken cancellationToken = default);
}

public interface IArtifactWriter
{
    Task<RunArtifactPaths> PrepareRunAsync(
        string outputRoot,
        string modeName,
        string runId,
        CancellationToken cancellationToken = default);

    Task<RunArtifactPaths> PrepareStageAsync(
        RunArtifactPaths root,
        int stageIndex,
        string modeName,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists one phase output under <c>&lt;baseDirectory&gt;/phases/</c> as
    /// <c>NN-&lt;phase&gt;.md</c>. <paramref name="baseDirectory"/> is the run
    /// directory for simple patterns or a stage directory for pipelines; it must
    /// be contained by the run directory. Returns the written file path.
    /// </summary>
    Task<string> WritePhaseAsync(
        RunArtifactPaths artifacts,
        string baseDirectory,
        int index,
        string phaseName,
        string content,
        CancellationToken cancellationToken = default);
}

public interface ITraceSession
{
    string FilePath { get; }

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

public interface IEvalRunner
{
    Task<EvalReport> EvaluateAsync(
        EvalContext context,
        CancellationToken cancellationToken = default);
}

public interface IRunViewWriter
{
    Task<string> WriteAsync(
        RunViewModel viewModel,
        CancellationToken cancellationToken = default);
}

public interface IRunStateStore
{
    Task UpsertRunAsync(
        RunCatalogEntry entry,
        CancellationToken cancellationToken = default);

    Task<RunCatalogEntry?> GetRunAsync(
        string runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunCatalogEntry>> ListRunsAsync(
        CancellationToken cancellationToken = default);
}

public interface IArtifactStore
{
    Task PutAsync(
        StoredRunArtifact artifact,
        CancellationToken cancellationToken = default);

    Task<StoredRunArtifact?> GetAsync(
        string runId,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredRunArtifactDescriptor>> ListAsync(
        string runId,
        CancellationToken cancellationToken = default);
}

public interface ISemanticIndex
{
    Task UpsertAsync(
        IReadOnlyList<SemanticIndexDocument> documents,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticSearchMatch>> SearchAsync(
        SemanticSearchQuery query,
        CancellationToken cancellationToken = default);

    Task DeleteRunAsync(
        string runId,
        CancellationToken cancellationToken = default);
}
