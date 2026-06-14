using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Tools;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly ToolPolicy _policy;

    public ToolExecutor(ToolPolicy policy)
    {
        _policy = policy;
    }

    public async Task<ToolResult> ExecuteAsync(
        IToolProvider provider,
        ToolRequest request,
        ITraceSession trace,
        CancellationToken cancellationToken = default)
    {
        var decision = _policy.Evaluate(request);
        await trace.EmitAsync(
            TraceEventNames.ToolPolicyEvaluated,
            new Dictionary<string, object?>
            {
                ["provider"] = provider.ProviderName,
                ["tool"] = request.Tool.Name,
                ["category"] = request.Tool.Category.ToString().ToLowerInvariant(),
                ["allowed"] = decision.Allowed,
                ["reason"] = decision.Reason,
                ["heavyTracing"] = decision.RequiresHeavyTracing
            },
            cancellationToken);

        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Reason);
        }

        await trace.EmitAsync(
            TraceEventNames.ToolCalled,
            new Dictionary<string, object?>
            {
                ["provider"] = provider.ProviderName,
                ["tool"] = request.Tool.Name,
                ["arguments"] = request.Arguments
            },
            cancellationToken);

        var result = await provider.ExecuteAsync(request, cancellationToken);

        await trace.EmitAsync(
            TraceEventNames.ToolCompleted,
            new Dictionary<string, object?>
            {
                ["provider"] = provider.ProviderName,
                ["tool"] = request.Tool.Name,
                ["success"] = result.Success
            },
            cancellationToken);

        return result;
    }
}
