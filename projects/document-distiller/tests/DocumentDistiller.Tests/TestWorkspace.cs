namespace DocumentDistiller.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "DocumentDistiller.Tests",
            Guid.NewGuid().ToString("N"));
        InputRoot = Path.Combine(Root, "input");
        OutputRoot = Path.Combine(Root, "outputs");
        PromptsRoot = Path.Combine(Root, "prompts");
        Directory.CreateDirectory(InputRoot);
        Directory.CreateDirectory(OutputRoot);
        Directory.CreateDirectory(PromptsRoot);

        File.WriteAllText(Path.Combine(PromptsRoot, "analyze.md"), "Analyze.");
        File.WriteAllText(Path.Combine(PromptsRoot, "critic.md"), "Critique.");
        File.WriteAllText(Path.Combine(PromptsRoot, "revise.md"), "Revise.");
    }

    public string Root { get; }

    public string InputRoot { get; }

    public string OutputRoot { get; }

    public string PromptsRoot { get; }

    public void AddDocument(string name, string content) =>
        File.WriteAllText(Path.Combine(InputRoot, name), content);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _value;

    public FixedTimeProvider(DateTimeOffset value)
    {
        _value = value;
    }

    public override DateTimeOffset GetUtcNow() => _value;
}
