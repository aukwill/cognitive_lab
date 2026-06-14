using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Tests;

public sealed class ArtifactWriterTests
{
    [Fact]
    public async Task PrepareRunAsync_CreatesTimestampedRunDirectory()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(
            new FixedTimeProvider(
                new DateTimeOffset(2026, 6, 13, 12, 34, 56, TimeSpan.Zero)));

        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "1234567890");

        Assert.True(Directory.Exists(artifacts.RunDirectory));
        Assert.Contains("20260613T123456000Z_frame_12345678", artifacts.RunDirectory);
    }

    [Fact]
    public async Task PrepareRunAsync_RejectsExistingRunDirectory()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(
            new FixedTimeProvider(
                new DateTimeOffset(2026, 6, 13, 12, 34, 56, TimeSpan.Zero)));

        var first = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "run-001");

        var exception = await Assert.ThrowsAsync<IOException>(
            () => writer.PrepareRunAsync(
                workspace.OutputRoot,
                "frame",
                "run-001"));

        Assert.Contains(first.RunDirectory, exception.Message);
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task WriteAsync_RejectsPathOutsideRunDirectory()
    {
        using var workspace = new TestWorkspace();
        var writer = new ArtifactWriter(TimeProvider.System);
        var artifacts = await writer.PrepareRunAsync(
            workspace.OutputRoot,
            "frame",
            "12345678");
        var escaped = artifacts with
        {
            ResultPath = Path.Combine(workspace.Root, "escaped.md")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(escaped, ArtifactKind.Result, "blocked"));
    }
}
