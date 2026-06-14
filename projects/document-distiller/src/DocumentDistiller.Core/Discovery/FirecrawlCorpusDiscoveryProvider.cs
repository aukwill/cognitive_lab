using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;

namespace DocumentDistiller.Core.Discovery;

public sealed class FirecrawlCorpusDiscoveryProvider : ICorpusDiscoveryProvider
{
    private readonly HttpClient _httpClient;
    private readonly FirecrawlOptions _options;

    public FirecrawlCorpusDiscoveryProvider(
        HttpClient httpClient,
        FirecrawlOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderName => "firecrawl";

    public async Task<IReadOnlyList<CorpusSearchResult>> SearchAsync(
        CorpusSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();

        var payload = new Dictionary<string, object?>
        {
            ["query"] = request.Query,
            ["limit"] = request.Limit,
            ["sources"] = new[] { "web" }
        };
        if (request.IncludeDomains.Count > 0)
        {
            payload["includeDomains"] = request.IncludeDomains;
        }

        using var response = await SendAsync(
            "search",
            payload,
            cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var results = data.ValueKind == JsonValueKind.Array
            ? data
            : data.GetProperty("web");

        var rank = 0;
        return results
            .EnumerateArray()
            .Select(
                item => new CorpusSearchResult(
                    item.TryGetProperty("position", out var position)
                        ? position.GetInt32()
                        : ++rank,
                    GetString(item, "title") ?? "Untitled source",
                    GetString(item, "url")
                        ?? throw new InvalidOperationException(
                            "Firecrawl returned a search result without a URL."),
                    GetString(item, "description") ?? string.Empty))
            .ToArray();
    }

    public async Task<CorpusPage> FetchAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        EnsureConfigured();

        using var response = await SendAsync(
            "scrape",
            new
            {
                url,
                formats = new[] { "markdown" },
                onlyMainContent = true
            },
            cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var metadata = data.TryGetProperty("metadata", out var metadataValue)
            ? metadataValue
            : default;
        var title = metadata.ValueKind == JsonValueKind.Object
            ? GetString(metadata, "title")
            : null;

        return new CorpusPage(
            title ?? "Untitled source",
            metadata.ValueKind == JsonValueKind.Object
                ? GetString(metadata, "sourceURL") ?? url
                : url,
            GetString(data, "markdown")
                ?? throw new InvalidOperationException(
                    "Firecrawl returned a scraped page without Markdown."));
    }

    private async Task<HttpResponseMessage> SendAsync(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.Endpoint.TrimEnd('/')}/v2/{path}")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = (int)response.StatusCode;
        response.Dispose();
        throw new InvalidOperationException(
            $"Firecrawl {path} request failed with HTTP {status}. " +
            "The response body was omitted to avoid exposing provider data.");
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("success", out var success) ||
            !success.GetBoolean())
        {
            document.Dispose();
            throw new InvalidOperationException(
                "Firecrawl returned an unsuccessful response.");
        }

        return document;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                $"Firecrawl discovery requires {_options.ApiKeyEnvironmentVariable}.");
        }
    }
}
