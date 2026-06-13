using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Runtime;

public sealed class PhaseRunner
{
    public async Task<PhaseResult> RunAsync(
        string runId,
        LoadedMode mode,
        LoadedPhase phase,
        string input,
        IReadOnlyList<PhaseResult> priorPhaseResults,
        IModelClient modelClient,
        ITraceSession trace,
        CancellationToken cancellationToken = default)
    {
        await trace.EmitAsync(
            "phase.started",
            new Dictionary<string, object?>
            {
                ["mode"] = mode.Manifest.Name,
                ["phase"] = phase.Name,
                ["kind"] = phase.Kind.ToString().ToLowerInvariant()
            },
            cancellationToken);

        if (phase.Kind == PhaseKind.Critic)
        {
            await trace.EmitAsync(
                "critic.started",
                new Dictionary<string, object?> { ["phase"] = phase.Name },
                cancellationToken);
        }
        else if (phase.Kind == PhaseKind.Revision)
        {
            await trace.EmitAsync(
                "revision.started",
                new Dictionary<string, object?>
                {
                    ["phase"] = phase.Name,
                    ["priorPhaseCount"] = priorPhaseResults.Count
                },
                cancellationToken);
        }

        await trace.EmitAsync(
            "model.called",
            new Dictionary<string, object?>
            {
                ["provider"] = modelClient.ProviderName,
                ["phase"] = phase.Name
            },
            cancellationToken);

        var response = await modelClient.CompleteAsync(
            new ModelRequest(
                runId,
                mode.Manifest.Name,
                phase.Name,
                phase.Kind,
                phase.Prompt,
                input,
                Array.AsReadOnly(priorPhaseResults.ToArray())),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            throw new ModelProviderException(
                $"Provider '{modelClient.ProviderName}' returned empty content for phase '{phase.Name}'.");
        }

        await trace.EmitAsync(
            "model.completed",
            new Dictionary<string, object?>
            {
                ["provider"] = response.Provider,
                ["model"] = response.Model,
                ["phase"] = phase.Name,
                ["contentLength"] = response.Content.Length
            },
            cancellationToken);

        if (phase.Kind == PhaseKind.Critic)
        {
            await trace.EmitAsync(
                "critic.completed",
                new Dictionary<string, object?>
                {
                    ["phase"] = phase.Name,
                    ["contentLength"] = response.Content.Length
                },
                cancellationToken);
        }
        else if (phase.Kind == PhaseKind.Revision)
        {
            await trace.EmitAsync(
                "revision.completed",
                new Dictionary<string, object?>
                {
                    ["phase"] = phase.Name,
                    ["contentLength"] = response.Content.Length
                },
                cancellationToken);
        }

        return new PhaseResult(
            phase.Name,
            phase.Kind,
            response.Content,
            response.Provider,
            response.Model);
    }
}
