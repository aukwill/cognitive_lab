using System.Net.Http.Json;
using System.Text.Json;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Models;

/// <summary>
/// Calls the Azure OpenAI / Microsoft Foundry Responses API
/// (<c>POST /openai/v1/responses</c>) using the same request/response shape
/// as <see cref="OpenRouterModelClient"/>.
/// </summary>
public sealed class AzureFoundryModelClient : IModelClient
{
    private readonly HttpClient _httpClient;
    private readonly AzureFoundryOptions _options;

    public AzureFoundryModelClient(
        HttpClient httpClient,
        AzureFoundryOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderName => "azure-foundry";

    public async Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            GetResponsesUri());
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        httpRequest.Content = JsonContent.Create(new
        {
            model = _options.Deployment,
            instructions = request.Prompt,
            input = ModelRequestFormatting.BuildUserMessage(request),
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
                "Azure Foundry request failed before a response was received.",
                exception);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ModelProviderException(
                "Azure Foundry request timed out before a response was received.",
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
                        "Azure Foundry returned an empty completion.");
                }

                return new ModelResponse(
                    content,
                    ProviderName,
                    _options.Deployment);
            }
            catch (JsonException exception)
            {
                throw new ModelProviderException(
                    "Azure Foundry returned malformed JSON.",
                    exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new ModelProviderException(
                    "Azure Foundry response did not contain a text completion.",
                    exception);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ModelProviderException(
                    "Azure Foundry response did not contain a text completion.",
                    exception);
            }
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint)
            || !Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out _))
        {
            throw new ModelProviderException(
                "AZURE_FOUNDRY_ENDPOINT must be set to an absolute URI for the azure-foundry provider.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ModelProviderException(
                "AZURE_FOUNDRY_API_KEY is required for the azure-foundry provider.");
        }

        if (string.IsNullOrWhiteSpace(_options.Deployment))
        {
            throw new ModelProviderException(
                "AZURE_FOUNDRY_DEPLOYMENT is required for the azure-foundry provider.");
        }
    }

    private Uri GetResponsesUri()
    {
        var endpoint = _options.Endpoint!.TrimEnd('/');

        if (!endpoint.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            if (!endpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            {
                endpoint += "/openai/v1";
            }

            endpoint += "/responses";
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiVersion))
        {
            endpoint += $"?api-version={Uri.EscapeDataString(_options.ApiVersion)}";
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
                " Check that AZURE_FOUNDRY_API_KEY is valid.",
            System.Net.HttpStatusCode.NotFound =>
                " Check that AZURE_FOUNDRY_ENDPOINT and AZURE_FOUNDRY_DEPLOYMENT are correct.",
            System.Net.HttpStatusCode.TooManyRequests =>
                " Azure Foundry rate limits or quota may have been exceeded.",
            System.Net.HttpStatusCode.BadRequest =>
                " Check that AZURE_FOUNDRY_DEPLOYMENT is a valid deployment name and supports the Responses API.",
            _ => string.Empty
        };

        return $"Azure Foundry returned {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}).{guidance} Response: " +
            $"{ModelRequestFormatting.Abbreviate(responseBody)}";
    }
}
