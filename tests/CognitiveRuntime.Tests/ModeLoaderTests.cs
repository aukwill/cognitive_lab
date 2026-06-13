using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Modes;

namespace CognitiveRuntime.Tests;

public sealed class ModeLoaderTests
{
    [Fact]
    public async Task LoadAsync_LoadsManifestDocumentationAndPrompts()
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var loader = new FileModeLoader(workspace.ModesRoot);

        var mode = await loader.LoadAsync("frame");

        Assert.Equal("frame", mode.Manifest.Name);
        Assert.Contains("# frame", mode.Documentation);
        Assert.Collection(
            mode.Phases,
            phase =>
            {
                Assert.Equal("main", phase.Name);
                Assert.Equal(PhaseKind.Main, phase.Kind);
                Assert.NotEmpty(phase.Prompt);
            },
            phase =>
            {
                Assert.Equal("critic", phase.Name);
                Assert.Equal(PhaseKind.Critic, phase.Kind);
                Assert.NotEmpty(phase.Prompt);
            });
    }

    [Theory]
    [InlineData("../frame")]
    [InlineData("frame/child")]
    [InlineData("frame\\child")]
    public async Task LoadAsync_RejectsUnsafeModeNames(string modeName)
    {
        using var workspace = new TestWorkspace();
        workspace.CreateMode();
        var loader = new FileModeLoader(workspace.ModesRoot);

        var exception = await Assert.ThrowsAsync<ModeLoadException>(
            () => loader.LoadAsync(modeName));

        Assert.Contains("unsupported characters", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_RequiresCriticPromptFile()
    {
        using var workspace = new TestWorkspace();
        var modeDirectory = workspace.CreateMode();
        File.Delete(Path.Combine(modeDirectory, "prompts", "critic.md"));
        var loader = new FileModeLoader(workspace.ModesRoot);

        var exception = await Assert.ThrowsAsync<ModeLoadException>(
            () => loader.LoadAsync("frame"));

        Assert.Contains("prompts", exception.Message);
        Assert.Contains("critic.md", exception.Message);
    }
}
