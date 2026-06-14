using DocumentDistiller.Core.Artifacts;

namespace DocumentDistiller.Tests;

public sealed class RunArtifactWriterTests
{
    [Fact]
    public async Task WriteTextAsync_RejectsPathOutsideRunDirectory()
    {
        using var workspace = new TestWorkspace();
        var writer = new RunArtifactWriter(TimeProvider.System);
        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "12345678");
        var escapedPath = Path.Combine(workspace.Root, "escaped.md");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteTextAsync(
                artifacts,
                escapedPath,
                "blocked"));
    }
}
