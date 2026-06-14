using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.IO;
using CognitiveRuntime.Core.Persistence;

namespace CognitiveRuntime.Core.Tracing;

public sealed class JsonTraceSessionFactory : ITraceSessionFactory
{
    private readonly TimeProvider _timeProvider;
    private readonly IArtifactStore _artifactStore;

    public JsonTraceSessionFactory(
        TimeProvider timeProvider,
        IArtifactStore? artifactStore = null)
    {
        _timeProvider = timeProvider;
        _artifactStore = artifactStore ?? new NullArtifactStore();
    }

    public async Task<ITraceSession> CreateAsync(
        string runId,
        string tracePath,
        CancellationToken cancellationToken = default)
    {
        var session = new JsonTraceSession(
            runId,
            tracePath,
            _timeProvider,
            _artifactStore);
        await session.InitializeAsync(cancellationToken);
        return session;
    }
}

internal sealed class JsonTraceSession : ITraceSession
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly List<TraceEvent> _events = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _runId;
    private readonly TimeProvider _timeProvider;
    private readonly IArtifactStore _artifactStore;

    public JsonTraceSession(
        string runId,
        string tracePath,
        TimeProvider timeProvider,
        IArtifactStore artifactStore)
    {
        _runId = runId;
        FilePath = Path.GetFullPath(tracePath);
        _timeProvider = timeProvider;
        _artifactStore = artifactStore;
    }

    public string FilePath { get; }

    public IReadOnlyList<TraceEvent> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToArray();
            }
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        PersistAsync(cancellationToken);

    public async Task EmitAsync(
        string eventType,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_events)
            {
                var terminalEvent = _events.FirstOrDefault(
                    traceEvent => TraceEventNames.IsTerminal(traceEvent.Type));
                if (terminalEvent is not null)
                {
                    throw new InvalidOperationException(
                        $"Trace '{_runId}' is already terminal at sequence " +
                        $"'{terminalEvent.Sequence}' with event " +
                        $"'{terminalEvent.Type}'.");
                }

                var sequence = _events.Count + 1L;
                _events.Add(
                    new TraceEvent(
                        sequence,
                        _timeProvider.GetUtcNow(),
                        eventType,
                        _runId,
                        data ?? new Dictionary<string, object?>()));
            }

            await PersistAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        TraceEvent[] snapshot;
        lock (_events)
        {
            snapshot = [.. _events];
        }

        var document = new TraceDocument(
            TraceSchema.CurrentVersion,
            _runId,
            snapshot);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await FilePersistence.WriteAllTextAtomicAsync(
            FilePath,
            json,
            cancellationToken);
        await _artifactStore.PutAsync(
            StoredRunArtifactFactory.CreateText(
                _runId,
                Path.GetDirectoryName(FilePath)
                    ?? throw new InvalidOperationException(
                        $"Trace path '{FilePath}' has no parent directory."),
                FilePath,
                "application/json; charset=utf-8",
                json,
                _timeProvider.GetUtcNow()),
            cancellationToken);
    }
}
