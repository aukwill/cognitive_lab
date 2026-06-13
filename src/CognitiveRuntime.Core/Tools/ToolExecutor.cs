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
            "tool.policy_evaluated",
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
            "tool.called",
            new Dictionary<string, object?>
            {
                ["provider"] = provider.ProviderName,
                ["tool"] = request.Tool.Name,
                ["arguments"] = request.Arguments
            },
            cancellationToken);

        var result = await provider.ExecuteAsync(request, cancellationToken);

        await trace.EmitAsync(
            "tool.completed",
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
