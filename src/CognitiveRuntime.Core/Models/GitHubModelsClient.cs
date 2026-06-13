using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Models;

public sealed class GitHubModelsClient : IModelClient
{
    private readonly HttpClient _httpClient;
    private readonly GitHubModelsOptions _options;

    public GitHubModelsClient(
        HttpClient httpClient,
        GitHubModelsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderName => "github-models";

    public async Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            GetChatCompletionsUri());
        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.Token);
        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        httpRequest.Content = JsonContent.Create(new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = request.Prompt },
                new { role = "user", content = BuildUserMessage(request) }
            },
            temperature = 0.2
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ModelProviderException(
                "GitHub Models request failed before a response was received.",
                exception);
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ModelProviderException(
                    $"GitHub Models returned {(int)response.StatusCode} " +
                    $"({response.ReasonPhrase}): {Abbreviate(responseBody)}");
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var content = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new ModelProviderException(
                        "GitHub Models returned an empty completion.");
                }

                return new ModelResponse(
                    content,
                    ProviderName,
                    _options.Model);
            }
            catch (JsonException exception)
            {
                throw new ModelProviderException(
                    "GitHub Models returned malformed JSON.",
                    exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new ModelProviderException(
                    "GitHub Models response did not contain a text completion.",
                    exception);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ModelProviderException(
                    "GitHub Models response did not contain a text completion.",
                    exception);
            }
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new ModelProviderException(
                "GITHUB_TOKEN is required for the github-models provider.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new ModelProviderException(
                "GITHUB_MODELS_MODEL is required for the github-models provider.");
        }

        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out _))
        {
            throw new ModelProviderException(
                "GITHUB_MODELS_ENDPOINT must be an absolute URI.");
        }
    }

    private Uri GetChatCompletionsUri()
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += "/chat/completions";
        }

        return new Uri(endpoint, UriKind.Absolute);
    }

    private static string BuildUserMessage(ModelRequest request)
    {
        var previousOutput = string.IsNullOrWhiteSpace(request.PreviousOutput)
            ? "None"
            : request.PreviousOutput;

        return $"""
            Mode: {request.ModeName}
            Phase: {request.PhaseName}

            Original input:
            {request.Input}

            Previous phase output:
            {previousOutput}
            """;
    }

    private static string Abbreviate(string value)
    {
        const int limit = 800;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= limit
            ? normalized
            : string.Concat(normalized.AsSpan(0, limit), "...");
    }
}
