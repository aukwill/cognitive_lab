using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Tools;

public sealed class ToolPolicy
{
    private readonly HashSet<string> _allowlist;

    public ToolPolicy(IEnumerable<string> allowlist)
    {
        _allowlist = allowlist.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public ToolPolicyDecision Evaluate(ToolRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Tool.Category == ToolCategory.Execute)
        {
            return new ToolPolicyDecision(
                false,
                "Execute tools are blocked by default and cannot be enabled in the MVP.");
        }

        if (!_allowlist.Contains(request.Tool.Name))
        {
            return new ToolPolicyDecision(
                false,
                $"Tool '{request.Tool.Name}' is not allowlisted.");
        }

        return request.Tool.Category switch
        {
            ToolCategory.Read => new ToolPolicyDecision(
                true,
                "Read tool is allowlisted."),
            ToolCategory.Write => EvaluateWrite(request),
            ToolCategory.External => new ToolPolicyDecision(
                true,
                "External tool is allowlisted and requires detailed tracing.",
                RequiresHeavyTracing: true),
            _ => new ToolPolicyDecision(false, "Unknown tool category.")
        };
    }

    private static ToolPolicyDecision EvaluateWrite(ToolRequest request)
    {
        if (!request.ExplicitApproval)
        {
            return new ToolPolicyDecision(
                false,
                "Write tools require explicit runtime approval.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return new ToolPolicyDecision(
                false,
                "Write tools require a target path.");
        }

        var outputRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(request.OutputDirectory));
        var targetPath = Path.GetFullPath(request.TargetPath);
        var outputPrefix = outputRoot + Path.DirectorySeparatorChar;

        if (!targetPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolPolicyDecision(
                false,
                "Write target is outside the run output directory.");
        }

        return new ToolPolicyDecision(
            true,
            "Write tool is allowlisted, approved, and constrained to the run output directory.");
    }
}
