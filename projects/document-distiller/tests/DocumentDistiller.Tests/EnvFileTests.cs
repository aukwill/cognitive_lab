using DocumentDistiller.Cli;

namespace DocumentDistiller.Tests;

public sealed class EnvFileTests
{
    [Fact]
    public void Parse_ReadsKeyValuePairs()
    {
        var values = EnvFile.Parse(
            [
                "# comment",
                "",
                "FIRECRAWL_API_KEY=abc123",
                "OPENROUTER_MODEL=\"openai/gpt-5\"",
                "  SPACED = trimmed  ",
                "NOT_AN_ASSIGNMENT",
                "=missing-key"
            ]);

        Assert.Equal("abc123", values["FIRECRAWL_API_KEY"]);
        Assert.Equal("openai/gpt-5", values["OPENROUTER_MODEL"]);
        Assert.Equal("trimmed", values["SPACED"]);
        Assert.False(values.ContainsKey("NOT_AN_ASSIGNMENT"));
        Assert.False(values.ContainsKey(""));
    }

    [Fact]
    public void FindEnvFile_WalksUpToNearestAncestor()
    {
        using var workspace = new TestWorkspace();
        var nested = Path.Combine(workspace.Root, "a", "b", "c");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(workspace.Root, ".env"), "KEY=value");

        var found = EnvFile.FindEnvFile(nested);

        Assert.Equal(Path.Combine(workspace.Root, ".env"), found);
    }

    [Fact]
    public void FindEnvFile_ReturnsNullWhenNoneExists()
    {
        using var workspace = new TestWorkspace();

        var found = EnvFile.FindEnvFile(workspace.InputRoot);

        Assert.Null(found);
    }
}
