using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Runtime;

public sealed class FilePromptLoader : IPromptLoader
{
    public async Task<PromptSet> LoadAsync(
        string promptsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptsRoot);
        var root = Path.GetFullPath(promptsRoot);

        return new PromptSet(
            await ReadRequiredAsync(root, "analyze.md", cancellationToken),
            await ReadRequiredAsync(root, "critic.md", cancellationToken),
            await ReadRequiredAsync(root, "revise.md", cancellationToken));
    }

    private static async Task<string> ReadRequiredAsync(
        string root,
        string fileName,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Required prompt '{fileName}' was not found in '{root}'.",
                path);
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                $"Required prompt '{fileName}' is empty.");
        }

        return content.Trim();
    }
}
