using System.Text.Json;
using System.Text.Json.Serialization;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Modes;

public sealed class FileModeLoader : IModeLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _modesRoot;

    public FileModeLoader(string modesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modesRoot);
        _modesRoot = Path.GetFullPath(modesRoot);
    }

    public async Task<LoadedMode> LoadAsync(
        string modeName,
        CancellationToken cancellationToken = default)
    {
        ValidateModeName(modeName);

        var modeDirectory = ResolveChildPath(_modesRoot, modeName);
        if (!Directory.Exists(modeDirectory))
        {
            throw new ModeLoadException(
                $"Mode '{modeName}' was not found under '{_modesRoot}'.");
        }

        var documentationPath = RequireFile(modeDirectory, "MODE.md");
        var manifestPath = RequireFile(modeDirectory, "mode.json");
        RequireFile(modeDirectory, Path.Combine("prompts", "main.md"));
        RequireFile(modeDirectory, Path.Combine("prompts", "critic.md"));

        ModeManifest manifest;
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<ModeManifest>(json, JsonOptions)
                ?? throw new ModeLoadException(
                    $"Mode manifest '{manifestPath}' was empty.");
        }
        catch (JsonException exception)
        {
            throw new ModeLoadException(
                $"Mode manifest '{manifestPath}' is not valid JSON.",
                exception);
        }

        ValidateManifest(modeName, manifest);

        var phases = new List<LoadedPhase>(manifest.Phases.Count);
        foreach (var phase in manifest.Phases)
        {
            var promptPath = RequireFile(modeDirectory, phase.Prompt);
            var prompt = await File.ReadAllTextAsync(promptPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ModeLoadException(
                    $"Prompt '{phase.Prompt}' for phase '{phase.Name}' is empty.");
            }

            phases.Add(new LoadedPhase(phase.Name, phase.Kind, prompt));
        }

        var documentation = await File.ReadAllTextAsync(
            documentationPath,
            cancellationToken);

        return new LoadedMode(modeDirectory, documentation, manifest, phases);
    }

    private static void ValidateModeName(string modeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);

        if (modeName.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            throw new ModeLoadException(
                $"Mode name '{modeName}' contains unsupported characters.");
        }
    }

    private static void ValidateManifest(string requestedName, ModeManifest manifest)
    {
        if (!string.Equals(requestedName, manifest.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModeLoadException(
                $"Mode manifest name '{manifest.Name}' does not match requested mode '{requestedName}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Description))
        {
            throw new ModeLoadException("Mode description is required.");
        }

        if (manifest.Phases.Count < 2)
        {
            throw new ModeLoadException(
                "A mode must define at least a main phase and a critic phase.");
        }

        var duplicatePhase = manifest.Phases
            .GroupBy(phase => phase.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePhase is not null)
        {
            throw new ModeLoadException(
                $"Phase name '{duplicatePhase.Key}' is duplicated.");
        }

        foreach (var phase in manifest.Phases)
        {
            if (string.IsNullOrWhiteSpace(phase.Name) ||
                string.IsNullOrWhiteSpace(phase.Prompt))
            {
                throw new ModeLoadException(
                    "Every phase requires a name and prompt path.");
            }
        }

        var mainIndex = manifest.Phases.FindIndex(phase => phase.Kind == PhaseKind.Main);
        var criticIndex = manifest.Phases.FindIndex(phase => phase.Kind == PhaseKind.Critic);

        if (mainIndex < 0 || criticIndex < 0)
        {
            throw new ModeLoadException(
                "A mode must define both a main phase and a critic phase.");
        }

        if (criticIndex < mainIndex)
        {
            throw new ModeLoadException(
                "The critic phase must run after the main phase.");
        }

        if (manifest.OutputContract.MinimumLength < 1 ||
            manifest.OutputContract.RequiredHeadings.Count == 0 ||
            manifest.OutputContract.RequiredHeadings.Any(string.IsNullOrWhiteSpace))
        {
            throw new ModeLoadException(
                "The output contract requires a positive minimum length and at least one heading.");
        }
    }

    private static string RequireFile(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ModeLoadException(
                $"Mode file path '{relativePath}' must be relative.");
        }

        var path = ResolveChildPath(root, relativePath);
        if (!File.Exists(path))
        {
            throw new ModeLoadException(
                $"Required mode file '{relativePath}' was not found.");
        }

        return path;
    }

    private static string ResolveChildPath(string root, string relativePath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var rootPrefix = fullRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModeLoadException(
                $"Path '{relativePath}' escapes the mode directory.");
        }

        return candidate;
    }
}
