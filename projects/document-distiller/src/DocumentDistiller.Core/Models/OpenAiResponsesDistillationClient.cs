using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Serialization;

namespace DocumentDistiller.Core.Models;

public sealed class OpenAiResponsesDistillationClient : IDistillationModelClient
{
    private readonly HttpClient _httpClient;
    private readonly ResponsesApiOptions _options;

    public OpenAiResponsesDistillationClient(
        HttpClient httpClient,
        ResponsesApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderName => _options.ProviderName;

    public string ModelName => _options.Model;

    public Task<ModelCompletion<DistillationDraft>> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default) =>
        CreateStructuredResponseAsync<DistillationDraft>(
            request.Prompts.Analyze,
            new
            {
                documents = request.Documents.Select(
                    document => new
                    {
                        document.Id,
                        document.RelativePath,
                        document.Title
                    }),
                chunks = request.Chunks
            },
            "distillation_draft",
            DraftSchema(),
            cancellationToken);

    public Task<ModelCompletion<DistillationCritique>> CritiqueAsync(
        CritiqueRequest request,
        CancellationToken cancellationToken = default) =>
        CreateStructuredResponseAsync<DistillationCritique>(
            request.Prompts.Critic,
            new
            {
                draft = request.Draft,
                evidenceChunks = request.Chunks
            },
            "distillation_critique",
            CritiqueSchema(),
            cancellationToken);

    public Task<ModelCompletion<DistillationDraft>> ReviseAsync(
        RevisionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateStructuredResponseAsync<DistillationDraft>(
            request.Prompts.Revise,
            new
            {
                draft = request.Draft,
                critique = request.Critique,
                evidenceChunks = request.Chunks
            },
            "distillation_revision",
            DraftSchema(),
            cancellationToken);

    private async Task<ModelCompletion<T>> CreateStructuredResponseAsync<T>(
        string developerPrompt,
        object payload,
        string schemaName,
        object schema,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                $"{_options.ApiKeyEnvironmentVariable} is required for the " +
                $"{ProviderName} provider.");
        }

        var body = new
        {
            model = _options.Model,
            input = new object[]
            {
                new
                {
                    role = "developer",
                    content = developerPrompt
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(payload, JsonDefaults.Options)
                }
            },
            reasoning = new
            {
                effort = _options.ReasoningEffort
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = schemaName,
                    strict = true,
                    schema
                }
            },
            store = false
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildResponsesEndpoint());
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (ProviderName == "openrouter")
        {
            request.Headers.TryAddWithoutValidation(
                "X-Title",
                "Document Distiller");
        }
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonDefaults.Options),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{ProviderName} Responses API returned " +
                $"{(int)response.StatusCode}: " +
                $"{ExtractErrorMessage(responseContent)}");
        }

        using var responseDocument = JsonDocument.Parse(responseContent);
        var outputText = ExtractOutputText(responseDocument.RootElement);
        var value = JsonSerializer.Deserialize<T>(outputText, JsonDefaults.Options)
            ?? throw new InvalidOperationException(
                "OpenAI returned an empty structured response.");
        return new ModelCompletion<T>(
            value,
            ExtractMetadata(responseDocument.RootElement));
    }

    private string BuildResponsesEndpoint()
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        return endpoint.EndsWith("/responses", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : $"{endpoint}/responses";
    }

    private static string ExtractOutputText(JsonElement root)
    {
        foreach (var output in root.GetProperty("output").EnumerateArray())
        {
            if (!output.TryGetProperty("content", out var content))
            {
                continue;
            }

            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    item.TryGetProperty("text", out var text))
                {
                    return text.GetString()
                        ?? throw new InvalidOperationException(
                            "OpenAI returned an empty output_text item.");
                }
            }
        }

        throw new InvalidOperationException(
            "OpenAI response did not contain output_text.");
    }

    private ModelInvocationMetadata ExtractMetadata(JsonElement root)
    {
        var usage = root.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        var cachedTokens = usage.ValueKind == JsonValueKind.Object &&
            usage.TryGetProperty("input_tokens_details", out var inputDetails)
            ? GetInt(inputDetails, "cached_tokens")
            : 0;
        var reasoningTokens = usage.ValueKind == JsonValueKind.Object &&
            usage.TryGetProperty("output_tokens_details", out var outputDetails)
            ? GetInt(outputDetails, "reasoning_tokens")
            : 0;

        return new ModelInvocationMetadata(
            ProviderName,
            root.TryGetProperty("model", out var model)
                ? model.GetString() ?? _options.Model
                : _options.Model,
            root.TryGetProperty("id", out var id) ? id.GetString() : null,
            GetInt(usage, "input_tokens", "prompt_tokens"),
            GetInt(usage, "output_tokens", "completion_tokens"),
            cachedTokens,
            reasoningTokens);
    }

    private static int GetInt(
        JsonElement element,
        string propertyName,
        string? fallbackPropertyName = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (element.TryGetProperty(propertyName, out var value) &&
            value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        return fallbackPropertyName is not null &&
            element.TryGetProperty(fallbackPropertyName, out var fallbackValue) &&
            fallbackValue.TryGetInt32(out var fallbackParsed)
                ? fallbackParsed
                : 0;
    }

    private static string ExtractErrorMessage(string responseContent)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            return document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString()
                ?? responseContent;
        }
        catch (JsonException)
        {
            return responseContent;
        }
    }

    private static object DraftSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[]
        {
            "title", "topic", "centralQuestion", "executiveSummary", "pillars",
            "crossCuttingThemes", "tensions", "gaps", "conclusion"
        },
        properties = new
        {
            title = StringSchema(),
            topic = StringSchema(),
            centralQuestion = StringSchema(),
            executiveSummary = StringSchema(),
            pillars = new
            {
                type = "array",
                minItems = 2,
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "id", "name", "thesis", "analysis", "claims"
                    },
                    properties = new
                    {
                        id = StringSchema(),
                        name = StringSchema(),
                        thesis = StringSchema(),
                        analysis = StringSchema(),
                        claims = new
                        {
                            type = "array",
                            minItems = 1,
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[]
                                {
                                    "id", "statement", "stance", "confidence",
                                    "evidenceIds"
                                },
                                properties = new
                                {
                                    id = StringSchema(),
                                    statement = StringSchema(),
                                    stance = new
                                    {
                                        type = "string",
                                        @enum = ClaimStances.All.ToArray()
                                    },
                                    confidence = new
                                    {
                                        type = "number",
                                        minimum = 0,
                                        maximum = 1
                                    },
                                    evidenceIds = StringArraySchema(minItems: 1)
                                }
                            }
                        }
                    }
                }
            },
            crossCuttingThemes = StringArraySchema(),
            tensions = StringArraySchema(),
            gaps = StringArraySchema(),
            conclusion = StringSchema()
        }
    };

    private static object CritiqueSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[]
        {
            "strengths", "issues", "missingEvidenceIds", "revisionGuidance"
        },
        properties = new
        {
            strengths = StringArraySchema(),
            issues = StringArraySchema(),
            missingEvidenceIds = StringArraySchema(),
            revisionGuidance = StringArraySchema()
        }
    };

    private static object StringSchema() => new { type = "string" };

    private static object StringArraySchema(int? minItems = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "array",
            ["items"] = StringSchema()
        };
        if (minItems.HasValue)
        {
            schema["minItems"] = minItems.Value;
        }

        return schema;
    }
}
