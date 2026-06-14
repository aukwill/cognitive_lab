using System.Text.Json;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Core.Models;

/// <summary>
/// Shared request/response formatting for model providers that expose an
/// OpenAI-compatible API.
/// </summary>
internal static class ModelRequestFormatting
{
    public static string BuildUserMessage(ModelRequest request)
    {
        var builder = new System.Text.StringBuilder()
            .Append("Mode: ")
            .AppendLine(request.ModeName)
            .Append("Phase: ")
            .AppendLine(request.PhaseName)
            .AppendLine()
            .AppendLine("Original input:")
            .AppendLine(request.Input)
            .AppendLine()
            .AppendLine("Prior phase results:");

        if (request.PriorPhaseResults.Count == 0)
        {
            return builder.AppendLine("None").ToString();
        }

        foreach (var result in request.PriorPhaseResults)
        {
            builder
                .AppendLine()
                .Append("Phase: ")
                .Append(result.PhaseName)
                .Append(" (")
                .Append(result.PhaseKind.ToString().ToLowerInvariant())
                .AppendLine(")")
                .AppendLine("Output:")
                .AppendLine(result.Content);
        }

        return builder.ToString();
    }

    public static string Abbreviate(string value)
    {
        const int limit = 800;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= limit
            ? normalized
            : string.Concat(normalized.AsSpan(0, limit), "...");
    }

    /// <summary>
    /// Concatenates the <c>output_text</c> content parts of the first
    /// <c>message</c> item in a Responses API <c>output</c> array.
    /// </summary>
    public static string? ExtractResponsesOutputText(JsonElement root)
    {
        foreach (var item in root.GetProperty("output").EnumerateArray())
        {
            if (item.GetProperty("type").GetString() != "message")
            {
                continue;
            }

            var texts = item.GetProperty("content").EnumerateArray()
                .Where(part => part.GetProperty("type").GetString() == "output_text")
                .Select(part => part.GetProperty("text").GetString() ?? string.Empty);

            return string.Concat(texts);
        }

        return null;
    }
}
