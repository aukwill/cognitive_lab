using CognitiveRuntime.Core.Contracts;
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

    Task WriteAsync(
        RunArtifactPaths artifacts,
        ArtifactKind kind,
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
