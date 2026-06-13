using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.IO;

namespace CognitiveRuntime.Core.Tracing;

public sealed class JsonTraceSessionFactory : ITraceSessionFactory
{
    private readonly TimeProvider _timeProvider;

    public JsonTraceSessionFactory(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async Task<ITraceSession> CreateAsync(
        string runId,
        string tracePath,
        CancellationToken cancellationToken = default)
    {
        var session = new JsonTraceSession(runId, tracePath, _timeProvider);
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

    public JsonTraceSession(
        string runId,
        string tracePath,
        TimeProvider timeProvider)
    {
        _runId = runId;
        FilePath = Path.GetFullPath(tracePath);
        _timeProvider = timeProvider;
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
                _events.Add(
                    new TraceEvent(
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

        var document = new TraceDocument(_runId, snapshot);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await FilePersistence.WriteAllTextAtomicAsync(
            FilePath,
            json,
            cancellationToken);
    }
}
