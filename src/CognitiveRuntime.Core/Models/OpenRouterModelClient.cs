using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Models;

public sealed class OpenRouterModelClient : IModelClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;

    public OpenRouterModelClient(
        HttpClient httpClient,
        OpenRouterOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderName => "openrouter";

    public async Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            GetResponsesUri());
        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        httpRequest.Content = JsonContent.Create(BuildRequestBody(request));

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
                "OpenRouter request failed before a response was received.",
                exception);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ModelProviderException(
                "OpenRouter request timed out before a response was received.",
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
                var content = ModelRequestFormatting.ExtractResponsesOutputText(document.RootElement);

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new ModelProviderException(
                        "OpenRouter returned an empty completion.");
                }

                return new ModelResponse(
                    content,
                    ProviderName,
                    _options.Model);
            }
            catch (JsonException exception)
            {
                throw new ModelProviderException(
                    "OpenRouter returned malformed JSON.",
                    exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new ModelProviderException(
                    "OpenRouter response did not contain a text completion.",
                    exception);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ModelProviderException(
                    "OpenRouter response did not contain a text completion.",
                    exception);
            }
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ModelProviderException(
                "OPENROUTER_API_KEY is required for the openrouter provider.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new ModelProviderException(
                "OPENROUTER_MODEL is required for the openrouter provider.");
        }

        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out _))
        {
            throw new ModelProviderException(
                "OPENROUTER_ENDPOINT must be an absolute URI.");
        }
    }

    private JsonObject BuildRequestBody(ModelRequest request)
    {
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["instructions"] = request.Prompt,
            ["input"] = ModelRequestFormatting.BuildUserMessage(request),
            ["temperature"] = 0.2
        };

        if (_options.EnableCodeExecution)
        {
            body["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "code_interpreter",
                    ["container"] = new JsonObject { ["type"] = "auto" }
                }
            };
        }

        return body;
    }

    private Uri GetResponsesUri()
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        if (!endpoint.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += "/responses";
        }

        return new Uri(endpoint, UriKind.Absolute);
    }

    private static string CreateFailureMessage(
        HttpResponseMessage response,
        string responseBody)
    {
        var guidance = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                " Check that OPENROUTER_API_KEY is valid.",
            System.Net.HttpStatusCode.TooManyRequests =>
                " OpenRouter rate limits may have been exceeded or credits may be exhausted.",
            System.Net.HttpStatusCode.BadRequest =>
                " Check that OPENROUTER_MODEL is a valid OpenRouter model ID.",
            _ => string.Empty
        };

        return $"OpenRouter returned {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}).{guidance} Response: " +
            $"{ModelRequestFormatting.Abbreviate(responseBody)}";
    }
}
