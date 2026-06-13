using System.Net;
using System.Text;
using System.Text.Json;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;

namespace CognitiveRuntime.Tests;

public sealed class GitHubModelsClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsVersionedRequestAndReturnsCompletion()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "A traced answer."
                  }
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubModelsClient(
            httpClient,
            new GitHubModelsOptions(
                "https://models.github.ai/inference",
                "test-token",
                "openai/gpt-4.1",
                "2026-03-10"));

        var response = await client.CompleteAsync(CreateRequest());

        Assert.Equal("A traced answer.", response.Content);
        Assert.Equal("github-models", response.Provider);
        Assert.Equal("openai/gpt-4.1", response.Model);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://models.github.ai/inference/chat/completions",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-token", handler.AuthorizationParameter);
        Assert.Contains("application/vnd.github+json", handler.AcceptMediaTypes);
        Assert.Equal("2026-03-10", handler.ApiVersion);
        Assert.Equal("application/json", handler.ContentType);

        using var requestBody = JsonDocument.Parse(
            Assert.IsType<string>(handler.Body));
        Assert.Equal(
            "openai/gpt-4.1",
            requestBody.RootElement.GetProperty("model").GetString());

        var messages = requestBody.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(
            "Follow the mode contract.",
            messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        var userMessage = Assert.IsType<string>(
                messages[1].GetProperty("content").GetString())
            .ReplaceLineEndings("\n");
        Assert.Contains(
            "Original input:\nFrame this runtime.",
            userMessage);
        Assert.Contains(
            "Prior phase results:",
            userMessage);
        Assert.Contains(
            "Phase: main (main)\nOutput:\nEarlier draft.",
            userMessage);
        Assert.Contains(
            "Phase: critic (critic)\nOutput:\nEarlier critique.",
            userMessage);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotDuplicateChatCompletionsPath()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"content"}}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubModelsClient(
            httpClient,
            new GitHubModelsOptions(
                "https://models.github.ai/inference/chat/completions",
                "test-token",
                "openai/gpt-4.1"));

        await client.CompleteAsync(CreateRequest());

        Assert.Equal(
            "https://models.github.ai/inference/chat/completions",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task CompleteAsync_RequiresTokenBeforeSendingRequest()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"content"}}]}""");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubModelsClient(
            httpClient,
            new GitHubModelsOptions(
                "https://models.github.ai/inference",
                null,
                "openai/gpt-4.1"));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("GITHUB_MODELS_TOKEN", exception.Message);
        Assert.Contains("GITHUB_TOKEN", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_ExplainsModelsReadPermissionOnForbidden()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.Forbidden,
            """{"message":"Resource not accessible by personal access token"}""");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubModelsClient(
            httpClient,
            new GitHubModelsOptions(
                "https://models.github.ai/inference",
                "secret-test-token",
                "openai/gpt-4.1"));

        var exception = await Assert.ThrowsAsync<ModelProviderException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("403", exception.Message);
        Assert.Contains("models:read", exception.Message);
        Assert.DoesNotContain("secret-test-token", exception.Message);
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

        public IReadOnlyList<string> AcceptMediaTypes { get; private set; } = [];

        public string? ApiVersion { get; private set; }

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
            AcceptMediaTypes = request.Headers.Accept
                .Select(value => value.MediaType ?? string.Empty)
                .ToArray();
            ApiVersion = request.Headers.GetValues("X-GitHub-Api-Version").Single();
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
