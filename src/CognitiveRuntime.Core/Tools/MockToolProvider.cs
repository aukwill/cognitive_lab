using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Tools;

public sealed class MockToolProvider : IToolProvider
{
    public string ProviderName => "mock";

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new ToolResult(
                true,
                $"Mock tool '{request.Tool.Name}' completed.",
                new Dictionary<string, object?>
                {
                    ["argumentCount"] = request.Arguments.Count
                }));
    }
}
