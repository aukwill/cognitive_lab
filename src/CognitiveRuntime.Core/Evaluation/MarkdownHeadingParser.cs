namespace CognitiveRuntime.Core.Evaluation;

public sealed record MarkdownHeading(int Level, string Text, int LineNumber);

/// <summary>
/// Parses ATX-style Markdown headings ("## Heading") structurally, skipping
/// headings that appear inside fenced code blocks.
/// </summary>
public static class MarkdownHeadingParser
{
    public static IReadOnlyList<MarkdownHeading> Parse(string content)
    {
        var headings = new List<MarkdownHeading>();
        var lines = content.Split('\n');
        var inFence = false;
        var fenceMarker = string.Empty;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (IsFenceDelimiter(trimmed, out var marker))
            {
                if (!inFence)
                {
                    inFence = true;
                    fenceMarker = marker;
                }
                else if (trimmed.StartsWith(fenceMarker, StringComparison.Ordinal))
                {
                    inFence = false;
                    fenceMarker = string.Empty;
                }

                continue;
            }

            if (inFence)
            {
                continue;
            }

            if (TryParseHeading(line, out var level, out var text))
            {
                headings.Add(new MarkdownHeading(level, text, i + 1));
            }
        }

        return headings;
    }

    /// <summary>
    /// Returns the trimmed body text between <paramref name="heading"/> and
    /// the next heading at any level (or the end of the document).
    /// </summary>
    public static string ExtractSectionContent(
        string content,
        MarkdownHeading heading,
        IReadOnlyList<MarkdownHeading> headings)
    {
        var lines = content.Split('\n');
        var startIndex = heading.LineNumber;
        var nextHeadingLine = headings
            .Where(candidate => candidate.LineNumber > heading.LineNumber)
            .Select(candidate => candidate.LineNumber)
            .DefaultIfEmpty(lines.Length + 1)
            .Min();
        var count = Math.Max(0, nextHeadingLine - startIndex - 1);

        return string.Join('\n', lines.Skip(startIndex).Take(count)).Trim();
    }

    /// <summary>
    /// Parses a single line as an ATX Markdown heading, returning its level
    /// (1-6) and trimmed text without the leading hashes or any optional
    /// trailing closing hash sequence.
    /// </summary>
    public static bool TryParseHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        var leadingSpaces = 0;
        while (leadingSpaces < line.Length && line[leadingSpaces] == ' ')
        {
            leadingSpaces++;
        }

        if (leadingSpaces > 3)
        {
            return false;
        }

        var remainder = line[leadingSpaces..];
        var hashCount = 0;
        while (hashCount < remainder.Length && remainder[hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount is 0 or > 6)
        {
            return false;
        }

        if (hashCount == remainder.Length)
        {
            level = hashCount;
            text = string.Empty;
            return true;
        }

        if (remainder[hashCount] != ' ' && remainder[hashCount] != '\t')
        {
            return false;
        }

        level = hashCount;
        text = StripTrailingHashes(remainder[(hashCount + 1)..].Trim());
        return true;
    }

    private static string StripTrailingHashes(string content)
    {
        var trimmedEnd = content.TrimEnd();
        var hashEnd = trimmedEnd.Length;
        while (hashEnd > 0 && trimmedEnd[hashEnd - 1] == '#')
        {
            hashEnd--;
        }

        if (hashEnd == trimmedEnd.Length || hashEnd == 0)
        {
            return trimmedEnd;
        }

        if (trimmedEnd[hashEnd - 1] is ' ' or '\t')
        {
            return trimmedEnd[..hashEnd].TrimEnd();
        }

        return trimmedEnd;
    }

    private static bool IsFenceDelimiter(string trimmedLine, out string marker)
    {
        if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
        {
            marker = "```";
            return true;
        }

        if (trimmedLine.StartsWith("~~~", StringComparison.Ordinal))
        {
            marker = "~~~";
            return true;
        }

        marker = string.Empty;
        return false;
    }
}
