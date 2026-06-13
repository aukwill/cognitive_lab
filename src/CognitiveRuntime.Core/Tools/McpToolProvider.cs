using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Tools;

public sealed class McpToolProvider : IToolProvider
{
    public string ProviderName => "mcp";

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(
            "MCP tool execution is an intentional MVP placeholder.");
    }
}
