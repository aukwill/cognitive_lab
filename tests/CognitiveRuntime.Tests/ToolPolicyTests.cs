using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Tools;

namespace CognitiveRuntime.Tests;

public sealed class ToolPolicyTests
{
    [Fact]
    public void Evaluate_BlocksNonAllowlistedRead()
    {
        var policy = new ToolPolicy([]);
        var request = CreateRequest("read-file", ToolCategory.Read);

        var decision = policy.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Contains("not allowlisted", decision.Reason);
    }

    [Fact]
    public void Evaluate_BlocksExecuteEvenWhenAllowlisted()
    {
        var policy = new ToolPolicy(["shell"]);
        var request = CreateRequest("shell", ToolCategory.Execute);

        var decision = policy.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Contains("blocked", decision.Reason);
    }

    [Fact]
    public void Evaluate_RequiresApprovalAndInRunPathForWrite()
    {
        using var workspace = new TestWorkspace();
        var policy = new ToolPolicy(["write-file"]);
        var outsidePath = Path.Combine(workspace.Root, "outside.txt");
        var request = CreateRequest(
            "write-file",
            ToolCategory.Write,
            workspace.OutputRoot,
            explicitApproval: true,
            targetPath: outsidePath);

        var decision = policy.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Contains("outside", decision.Reason);
    }

    [Fact]
    public void Evaluate_AllowsApprovedWriteInsideRunDirectory()
    {
        using var workspace = new TestWorkspace();
        var runDirectory = Path.Combine(workspace.OutputRoot, "run");
        Directory.CreateDirectory(runDirectory);
        var policy = new ToolPolicy(["write-file"]);
        var request = CreateRequest(
            "write-file",
            ToolCategory.Write,
            runDirectory,
            explicitApproval: true,
            targetPath: Path.Combine(runDirectory, "artifact.txt"));

        var decision = policy.Evaluate(request);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Evaluate_FlagsExternalToolsForHeavyTracing()
    {
        var policy = new ToolPolicy(["search"]);
        var request = CreateRequest("search", ToolCategory.External);

        var decision = policy.Evaluate(request);

        Assert.True(decision.Allowed);
        Assert.True(decision.RequiresHeavyTracing);
    }

    private static ToolRequest CreateRequest(
        string name,
        ToolCategory category,
        string? outputDirectory = null,
        bool explicitApproval = false,
        string? targetPath = null) =>
        new(
            new ToolDescriptor(name, category),
            new Dictionary<string, object?>(),
            outputDirectory ?? Path.GetTempPath(),
            explicitApproval,
            targetPath);
}
