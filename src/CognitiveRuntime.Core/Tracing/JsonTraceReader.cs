using System.Text.Json;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Tracing;

public sealed class JsonTraceReader
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<TraceDocument> ReadAsync(
        string tracePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracePath);

        await using var stream = File.OpenRead(Path.GetFullPath(tracePath));
        return await ReadAsync(stream, cancellationToken);
    }

    public async Task<TraceDocument> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var document = await JsonSerializer.DeserializeAsync<TraceDocument>(
                stream,
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidDataException("Trace document is empty.");

        Validate(document);
        return document;
    }

    private static void Validate(TraceDocument document)
    {
        if (document.SchemaVersion > TraceSchema.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Trace schema version '{document.SchemaVersion}' is newer " +
                $"than supported version '{TraceSchema.CurrentVersion}'.");
        }

        if (document.SchemaVersion != TraceSchema.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Trace schema version '{document.SchemaVersion}' is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(document.RunId);
        ArgumentNullException.ThrowIfNull(document.Events);

        for (var index = 0; index < document.Events.Count; index++)
        {
            var traceEvent = document.Events[index];
            var expectedSequence = index + 1L;
            if (traceEvent.Sequence != expectedSequence)
            {
                throw new InvalidDataException(
                    $"Trace event sequence must be contiguous from 1; " +
                    $"expected '{expectedSequence}' but found " +
                    $"'{traceEvent.Sequence}'.");
            }
        }
    }
}
