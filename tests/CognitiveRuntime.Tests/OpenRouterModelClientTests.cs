using System.Net;
using System.Text;
using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;

namespace CognitiveRuntime.Tests;

public sealed class OpenRouterModelClientTests
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
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1",
                "test-key",
                "openai/gpt-5"));

        var response = await client.CompleteAsync(CreateRequest());

        Assert.Equal("A traced answer.", response.Content);
        Assert.Equal("openrouter", response.Provider);
        Assert.Equal("openai/gpt-5", response.Model);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://openrouter.ai/api/v1/responses",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
        Assert.Equal("application/json", handler.ContentType);

        using var requestBody = JsonDocument.Parse(
            Assert.IsType<string>(handler.Body));
        Assert.Equal(
            "openai/gpt-5",
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
    public async Task CompleteAsync_OmitsToolsByDefault()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1",
                "test-key",
                "openai/gpt-5"));

        await client.CompleteAsync(CreateRequest());

        using var requestBody = JsonDocument.Parse(
            Assert.IsType<string>(handler.Body));
        Assert.False(requestBody.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task CompleteAsync_IncludesCodeInterpreterToolWhenEnabled()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1",
                "test-key",
                "openai/gpt-5",
                EnableCodeExecution: true));

        await client.CompleteAsync(CreateRequest());

        using var requestBody = JsonDocument.Parse(
            Assert.IsType<string>(handler.Body));
        var tools = requestBody.RootElement.GetProperty("tools");
        Assert.Equal(1, tools.GetArrayLength());
        var tool = tools[0];
        Assert.Equal("code_interpreter", tool.GetProperty("type").GetString());
        Assert.Equal("auto", tool.GetProperty("container").GetProperty("type").GetString());
    }

    [Fact]
    public async Task CompleteAsync_DoesNotDuplicateResponsesPath()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1/responses",
                "test-key",
                "openai/gpt-5"));

        await client.CompleteAsync(CreateRequest());

        Assert.Equal(
            "https://openrouter.ai/api/v1/responses",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task CompleteAsync_RequiresApiKeyBeforeSendingRequest()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1",
                null,
                "openai/gpt-5"));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("OPENROUTER_API_KEY", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_RequiresModelBeforeSendingRequest()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"content"}]}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1",
                "test-key",
                null));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("OPENROUTER_MODEL", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_ExplainsApiKeyOnUnauthorized()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Unauthorized,
            """{"error":{"message":"No auth credentials found"}}""");
        using var httpClient = new HttpClient(handler);
        var client = new OpenRouterModelClient(
            httpClient,
            new OpenRouterOptions(
                "https://openrouter.ai/api/v1",
                "secret-test-key",
                "openai/gpt-5"));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("401", exception.Message);
        Assert.Contains("OPENROUTER_API_KEY", exception.Message);
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

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? ContentType { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
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
