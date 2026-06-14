using System.Security.Cryptography;
using System.Text;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Ingestion;

public sealed class DocumentIngestor : IDocumentIngestor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md",
            ".markdown",
            ".txt"
        };

    public async Task<IngestionResult> IngestAsync(
        string inputDirectory,
        int maxInputCharacters,
        int chunkSizeCharacters,
        int chunkOverlapCharacters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInputCharacters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSizeCharacters, 200);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkOverlapCharacters);
        if (chunkOverlapCharacters >= chunkSizeCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkOverlapCharacters),
                "Chunk overlap must be smaller than the chunk size.");
        }

        var root = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Input directory '{root}' does not exist.");
        }

        var paths = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(
                path => Path.GetRelativePath(root, path),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
        {
            throw new InvalidOperationException(
                "No supported .md, .markdown, or .txt documents were found.");
        }

        var documents = new List<SourceDocument>();
        var totalCharacters = 0;

        for (var index = 0; index < paths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(paths[index], cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            totalCharacters += content.Length;
            if (totalCharacters > maxInputCharacters)
            {
                throw new InvalidOperationException(
                    $"The corpus contains {totalCharacters:N0} characters, exceeding " +
                    $"the configured limit of {maxInputCharacters:N0}.");
            }

            var relativePath = Path
                .GetRelativePath(root, paths[index])
                .Replace('\\', '/');
            documents.Add(
                new SourceDocument(
                    $"D{documents.Count + 1:D3}",
                    relativePath,
                    InferTitle(paths[index], content),
                    content,
                    Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(content)))));
        }

        if (documents.Count == 0)
        {
            throw new InvalidOperationException(
                "The input directory contained no non-empty supported documents.");
        }

        var chunks = documents
            .SelectMany(
                document => Chunk(
                    document,
                    chunkSizeCharacters,
                    chunkOverlapCharacters))
            .ToArray();

        return new IngestionResult(documents, chunks);
    }

    private static IEnumerable<DocumentChunk> Chunk(
        SourceDocument document,
        int chunkSizeCharacters,
        int chunkOverlapCharacters)
    {
        var chunks = new List<DocumentChunk>();
        var start = 0;
        var sequence = 1;

        while (start < document.Content.Length)
        {
            var hardEnd = Math.Min(
                start + chunkSizeCharacters,
                document.Content.Length);
            var end = hardEnd;
            if (hardEnd < document.Content.Length)
            {
                end = FindNaturalBoundary(
                    document.Content,
                    start,
                    hardEnd,
                    chunkSizeCharacters);
            }

            var content = document.Content[start..end];
            chunks.Add(
                new DocumentChunk(
                    $"{document.Id}-C{sequence:D3}",
                    document.Id,
                    sequence,
                    start,
                    end,
                    Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(content))),
                    content));

            if (end == document.Content.Length)
            {
                break;
            }

            start = Math.Max(start + 1, end - chunkOverlapCharacters);
            sequence++;
        }

        return chunks;
    }

    private static int FindNaturalBoundary(
        string content,
        int start,
        int hardEnd,
        int chunkSizeCharacters)
    {
        var minimumBoundary = start + (chunkSizeCharacters / 2);
        var paragraphBoundary = content.LastIndexOf(
            "\n\n",
            hardEnd - 1,
            hardEnd - start,
            StringComparison.Ordinal);
        if (paragraphBoundary >= minimumBoundary)
        {
            return paragraphBoundary + 2;
        }

        var lineBoundary = content.LastIndexOf(
            '\n',
            hardEnd - 1,
            hardEnd - start);
        if (lineBoundary >= minimumBoundary)
        {
            return lineBoundary + 1;
        }

        var whitespaceBoundary = content.LastIndexOf(
            ' ',
            hardEnd - 1,
            hardEnd - start);
        return whitespaceBoundary >= minimumBoundary
            ? whitespaceBoundary + 1
            : hardEnd;
    }

    private static string InferTitle(string path, string content)
    {
        var firstHeading = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));

        return firstHeading is null
            ? Path.GetFileNameWithoutExtension(path).Replace('-', ' ').Replace('_', ' ')
            : firstHeading[2..].Trim();
    }
}
