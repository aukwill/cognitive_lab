using System.Text.Json;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "CognitiveRuntime.Tests",
            Guid.NewGuid().ToString("N"));
        ModesRoot = Path.Combine(Root, "modes");
        OutputRoot = Path.Combine(Root, "outputs");

        Directory.CreateDirectory(ModesRoot);
        Directory.CreateDirectory(OutputRoot);
    }

    public string Root { get; }

    public string ModesRoot { get; }

    public string OutputRoot { get; }

    public string CreateMode(
        string name = "frame",
        IReadOnlyList<string>? requiredHeadings = null,
        IReadOnlyList<ModePhaseManifest>? phases = null)
    {
        requiredHeadings ??=
            ["## Problem", "## Objective", "## Constraints", "## Unknowns", "## Next Actions"];

        var modeDirectory = Path.Combine(ModesRoot, name);
        var promptsDirectory = Path.Combine(modeDirectory, "prompts");
        Directory.CreateDirectory(promptsDirectory);

        File.WriteAllText(
            Path.Combine(modeDirectory, "MODE.md"),
            $"# {name}{Environment.NewLine}");
        File.WriteAllText(
            Path.Combine(promptsDirectory, "main.md"),
            "Produce the declared output contract.");
        File.WriteAllText(
            Path.Combine(promptsDirectory, "critic.md"),
            "Critique the previous output.");
        File.WriteAllText(
            Path.Combine(promptsDirectory, "revision.md"),
            "Revise the draft using the critic while preserving the output contract.");

        var manifest = new ModeManifest
        {
            Name = name,
            Description = "Test mode.",
            Version = 1,
            Phases = phases?.ToList() ??
                [
                    new ModePhaseManifest
                    {
                        Name = "main",
                        Kind = PhaseKind.Main,
                        Prompt = "prompts/main.md"
                    },
                    new ModePhaseManifest
                    {
                        Name = "critic",
                        Kind = PhaseKind.Critic,
                        Prompt = "prompts/critic.md"
                    },
                    new ModePhaseManifest
                    {
                        Name = "revision",
                        Kind = PhaseKind.Revision,
                        Prompt = "prompts/revision.md"
                    }
                ],
            OutputContract = new OutputContract
            {
                RequiredHeadings = [.. requiredHeadings],
                MinimumLength = 100
            }
        };

        File.WriteAllText(
            Path.Combine(modeDirectory, "mode.json"),
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true,
                    Converters =
                    {
                        new System.Text.Json.Serialization.JsonStringEnumConverter(
                            JsonNamingPolicy.CamelCase)
                    }
                }));

        return modeDirectory;
    }

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
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _utcNow += duration;
    }
}
