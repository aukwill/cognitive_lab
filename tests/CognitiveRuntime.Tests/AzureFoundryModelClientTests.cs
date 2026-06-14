using System.Net;
using System.Text;
using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;

namespace CognitiveRuntime.Tests;

public sealed class AzureFoundryModelClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsRequestAndReturnsCompletion()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """
            {
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [
                    { "type": "output_text", "text": "A traced answer." }
                  ]
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                "https://example-resource.openai.azure.com",
                "test-key",
                "gpt-5",
                null));

        var response = await client.CompleteAsync(CreateRequest());

        Assert.Equal("A traced answer.", response.Content);
        Assert.Equal("azure-foundry", response.Provider);
        Assert.Equal("gpt-5", response.Model);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://example-resource.openai.azure.com/openai/v1/responses",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("test-key", handler.ApiKeyHeader);
        Assert.Equal("application/json", handler.ContentType);

        using var requestBody = JsonDocument.Parse(
            Assert.IsType<string>(handler.Body));
        Assert.Equal(
            "gpt-5",
            requestBody.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "Follow the mode contract.",
            requestBody.RootElement.GetProperty("instructions").GetString());

        var input = Assert.IsType<string>(
                requestBody.RootElement.GetProperty("input").GetString())
            .ReplaceLineEndings("\n");
        Assert.Contains(
            "Original input:\nFrame this runtime.",
            input);
    }

    [Fact]
    public async Task CompleteAsync_AppendsApiVersionWhenConfigured()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                "https://example-resource.openai.azure.com",
                "test-key",
                "gpt-5",
                "2026-03-01-preview"));

        await client.CompleteAsync(CreateRequest());

        Assert.Equal(
            "https://example-resource.openai.azure.com/openai/v1/responses?api-version=2026-03-01-preview",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotDuplicateResponsesPath()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                "https://example-resource.openai.azure.com/openai/v1/responses",
                "test-key",
                "gpt-5",
                null));

        await client.CompleteAsync(CreateRequest());

        Assert.Equal(
            "https://example-resource.openai.azure.com/openai/v1/responses",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task CompleteAsync_RequiresEndpointBeforeSendingRequest()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                null,
                "test-key",
                "gpt-5",
                null));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("AZURE_FOUNDRY_ENDPOINT", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_RequiresApiKeyBeforeSendingRequest()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                "https://example-resource.openai.azure.com",
                null,
                "gpt-5",
                null));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("AZURE_FOUNDRY_API_KEY", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_RequiresDeploymentBeforeSendingRequest()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                "https://example-resource.openai.azure.com",
                "test-key",
                null,
                null));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("AZURE_FOUNDRY_DEPLOYMENT", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_ExplainsApiKeyOnUnauthorized()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Unauthorized,
            """{"error":{"message":"Access denied due to invalid subscription key"}}""");
        using var httpClient = new HttpClient(handler);
        var client = new AzureFoundryModelClient(
            httpClient,
            new AzureFoundryOptions(
                "https://example-resource.openai.azure.com",
                "secret-test-key",
                "gpt-5",
                null));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("401", exception.Message);
        Assert.Contains("AZURE_FOUNDRY_API_KEY", exception.Message);
        Assert.DoesNotContain("secret-test-key", exception.Message);
    }

    private static ModelRequest CreateRequest() =>
        new(
            "run-1",
            "frame",
            "revision",
            PhaseKind.Revision,
            "Follow the mode contract.",
            "Frame this runtime.",
            Array.AsReadOnly(
                new[]
                {
                    new PhaseResult(
                        "main",
                        PhaseKind.Main,
                        "Earlier draft.",
                        "test",
                        "test-model"),
                    new PhaseResult(
                        "critic",
                        PhaseKind.Critic,
                        "Earlier critique.",
                        "test",
                        "test-model")
                }));

    private sealed class RecordingHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? ApiKeyHeader { get; private set; }

        public string? ContentType { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("api-key", out var values)
                ? values.FirstOrDefault()
                : null;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
