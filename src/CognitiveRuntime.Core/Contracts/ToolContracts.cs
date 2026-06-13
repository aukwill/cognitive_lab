namespace CognitiveRuntime.Core.Contracts;

public enum ToolCategory
{
    Read,
    Write,
    Execute,
    External
}

public sealed record ToolDescriptor(string Name, ToolCategory Category);

public sealed record ToolRequest(
    ToolDescriptor Tool,
    IReadOnlyDictionary<string, object?> Arguments,
    string OutputDirectory,
    bool ExplicitApproval = false,
    string? TargetPath = null);

public sealed record ToolResult(
    bool Success,
    string Content,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record ToolPolicyDecision(
    bool Allowed,
    string Reason,
    bool RequiresHeavyTracing = false);
