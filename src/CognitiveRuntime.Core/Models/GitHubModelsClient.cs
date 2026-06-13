using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Models;

public sealed class GitHubModelsClient : IModelClient
{
    private const string GitHubJsonMediaType = "application/vnd.github+json";
    private const string ApiVersionHeader = "X-GitHub-Api-Version";
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
            new MediaTypeWithQualityHeaderValue(GitHubJsonMediaType));
        httpRequest.Headers.Add(ApiVersionHeader, _options.ApiVersion);

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
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ModelProviderException(
                "GitHub Models request timed out before a response was received.",
                exception);
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ModelProviderException(
                    CreateFailureMessage(response, responseBody));
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
                "GITHUB_MODELS_TOKEN or GITHUB_TOKEN is required for the " +
                "github-models provider.");
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

        if (string.IsNullOrWhiteSpace(_options.ApiVersion))
        {
            throw new ModelProviderException(
                "GITHUB_MODELS_API_VERSION is required for the github-models provider.");
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

    private static string Abbreviate(string value)
    {
        const int limit = 800;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= limit
            ? normalized
            : string.Concat(normalized.AsSpan(0, limit), "...");
    }

    private static string CreateFailureMessage(
        HttpResponseMessage response,
        string responseBody)
    {
        var guidance = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                " Check that GITHUB_TOKEN is valid.",
            System.Net.HttpStatusCode.Forbidden =>
                " Check that the token has the models:read permission.",
            System.Net.HttpStatusCode.TooManyRequests =>
                " GitHub Models rate limits may have been exceeded.",
            _ => string.Empty
        };

        return $"GitHub Models returned {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}).{guidance} Response: {Abbreviate(responseBody)}";
    }
}
