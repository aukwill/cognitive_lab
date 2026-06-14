using System.Net;
using System.Text;
using System.Text.Json;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Models;

namespace DocumentDistiller.Tests;

public sealed class OpenAiResponsesDistillationClientTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesResponsesApiAndStrictStructuredOutput()
    {
        var draft = CreateDraft();
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            id = "resp_test",
                            model = "gpt-5.5-2026-06-01",
                            usage = new
                            {
                                input_tokens = 120,
                                output_tokens = 40,
                                input_tokens_details = new
                                {
                                    cached_tokens = 64
                                },
                                output_tokens_details = new
                                {
                                    reasoning_tokens = 12
                                }
                            },
                            output = new[]
                            {
                                new
                                {
                                    type = "message",
                                    content = new[]
                                    {
                                        new
                                        {
                                            type = "output_text",
                                            text = JsonSerializer.Serialize(draft)
                                        }
                                    }
                                }
                            }
                        }),
                    Encoding.UTF8,
                    "application/json")
            });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiResponsesDistillationClient(
            httpClient,
            new ResponsesApiOptions(
                "openai",
                "OPENAI_API_KEY",
                "https://api.openai.test/v1",
                "test-key",
                "gpt-5.5",
                "medium"));

        var result = await client.AnalyzeAsync(
            new AnalysisRequest(
                "run",
                new PromptSet("Analyze.", "Critique.", "Revise."),
                [
                    new SourceDocument(
                        "D001",
                        "one.md",
                        "One",
                        "Evidence",
                        "HASH"),
                    new SourceDocument(
                        "D002",
                        "two.md",
                        "Two",
                        "Evaluation",
                        "HASH")
                ],
                [
                    new DocumentChunk(
                        "D001-C001", "D001", 1, 0, 8, "HASH", "Evidence"),
                    new DocumentChunk(
                        "D002-C001", "D002", 1, 0, 10, "HASH", "Evaluation")
                ]));

        Assert.Equal("Evidence systems", result.Value.Topic);
        Assert.Equal("resp_test", result.Metadata.ResponseId);
        Assert.Equal(120, result.Metadata.InputTokens);
        Assert.Equal(64, result.Metadata.CachedInputTokens);
        Assert.Equal(12, result.Metadata.ReasoningTokens);
        Assert.Equal(
            "https://api.openai.test/v1/responses",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);

        using var requestJson = JsonDocument.Parse(handler.RequestBody!);
        var root = requestJson.RootElement;
        Assert.Equal("gpt-5.5", root.GetProperty("model").GetString());
        var format = root
            .GetProperty("text")
            .GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.False(
            format
                .GetProperty("schema")
                .GetProperty("properties")
                .GetProperty("crossCuttingThemes")
                .TryGetProperty("minItems", out _));
        var claimSchema = format
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("pillars")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("claims")
            .GetProperty("items");
        Assert.Contains(
            "corroborated",
            claimSchema
                .GetProperty("properties")
                .GetProperty("stance")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString()));
    }

    [Fact]
    public async Task CritiqueAsync_IncludesEvidenceChunkContent()
    {
        var critique = new DistillationCritique(
            ["Structured claims."],
            ["Check support."],
            [],
            ["Tighten wording."]);
        var handler = new RecordingHandler(
            CreateResponse(JsonSerializer.Serialize(critique)));
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiResponsesDistillationClient(
            httpClient,
            new ResponsesApiOptions(
                "openai",
                "OPENAI_API_KEY",
                "https://api.openai.test/v1",
                "test-key"));

        await client.CritiqueAsync(
            new CritiqueRequest(
                "run",
                new PromptSet("Analyze.", "Critique.", "Revise."),
                [
                    new SourceDocument(
                        "D001", "one.md", "One", "Exact evidence text.", "HASH")
                ],
                [
                    new DocumentChunk(
                        "D001-C001",
                        "D001",
                        1,
                        0,
                        20,
                        "HASH",
                        "Exact evidence text.")
                ],
                CreateDraft()));

        using var requestJson = JsonDocument.Parse(handler.RequestBody!);
        var payloadText = requestJson.RootElement
            .GetProperty("input")[1]
            .GetProperty("content")
            .GetString()!;
        using var payload = JsonDocument.Parse(payloadText);
        Assert.Equal(
            "Exact evidence text.",
            payload.RootElement
                .GetProperty("evidenceChunks")[0]
                .GetProperty("content")
                .GetString());
    }

    [Fact]
    public async Task AnalyzeAsync_CanTargetOpenRouterResponsesApi()
    {
        var handler = new RecordingHandler(
            CreateResponse(JsonSerializer.Serialize(CreateDraft())));
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiResponsesDistillationClient(
            httpClient,
            new ResponsesApiOptions(
                "openrouter",
                "OPENROUTER_API_KEY",
                "https://openrouter.ai/api/v1",
                "test-key",
                "openai/gpt-5"));

        var result = await client.AnalyzeAsync(
            new AnalysisRequest(
                "run",
                new PromptSet("Analyze.", "Critique.", "Revise."),
                [
                    new SourceDocument(
                        "D001", "one.md", "One", "Evidence", "HASH"),
                    new SourceDocument(
                        "D002", "two.md", "Two", "Evaluation", "HASH")
                ],
                [
                    new DocumentChunk(
                        "D001-C001", "D001", 1, 0, 8, "HASH", "Evidence"),
                    new DocumentChunk(
                        "D002-C001", "D002", 1, 0, 10, "HASH", "Evaluation")
                ]));

        Assert.Equal("openrouter", result.Metadata.Provider);
        Assert.Equal(
            "https://openrouter.ai/api/v1/responses",
            handler.Request!.RequestUri!.ToString());
        Assert.Contains(
            "Document Distiller",
            handler.Request.Headers.GetValues("X-Title"));
    }

    private static DistillationDraft CreateDraft() =>
        new(
            "Title",
            "Evidence systems",
            "How are evidence systems governed?",
            "Summary",
            [
                new Pillar(
                    "P01",
                    "Evidence",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C01",
                            "Evidence supports traceability.",
                            ClaimStances.SingleSource,
                            0.8,
                            ["D001-C001"])
                    ]),
                new Pillar(
                    "P02",
                    "Evaluation",
                    "Thesis",
                    "Analysis",
                    [
                        new EvidenceClaim(
                            "C02",
                            "Evaluation verifies evidence.",
                            ClaimStances.SingleSource,
                            0.8,
                            ["D002-C001"])
                    ])
            ],
            ["Traceability"],
            ["Trade-off"],
            ["Missing benchmark"],
            "Conclusion");

    private static HttpResponseMessage CreateResponse(string outputText) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        id = "resp_test",
                        model = "gpt-5.5",
                        output = new[]
                        {
                            new
                            {
                                type = "message",
                                content = new[]
                                {
                                    new
                                    {
                                        type = "output_text",
                                        text = outputText
                                    }
                                }
                            }
                        }
                    }),
                Encoding.UTF8,
                "application/json")
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}
