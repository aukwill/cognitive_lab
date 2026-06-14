using System.Net;
using System.Text;
using System.Text.Json;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Discovery;

namespace DocumentDistiller.Tests;

public sealed class FirecrawlCorpusDiscoveryProviderTests
{
    [Fact]
    public async Task SearchAndFetch_UseV2EndpointsAndMarkdownFormat()
    {
        var handler = new QueueHandler(
            CreateResponse(
                new
                {
                    success = true,
                    data = new
                    {
                        web = new[]
                        {
                            new
                            {
                                position = 2,
                                title = "Rules",
                                url = "https://rules.example/rules",
                                description = "Rules source"
                            }
                        }
                    }
                }),
            CreateResponse(
                new
                {
                    success = true,
                    data = new
                    {
                        markdown = "# Rules\nDanger should be legible.",
                        metadata = new
                        {
                            title = "Fetched Rules",
                            sourceURL = "https://rules.example/rules"
                        }
                    }
                }));
        using var httpClient = new HttpClient(handler);
        var provider = new FirecrawlCorpusDiscoveryProvider(
            httpClient,
            new FirecrawlOptions(
                "https://api.firecrawl.test",
                "fc-secret"));

        var results = await provider.SearchAsync(
            new CorpusSearchRequest(
                "meaningful danger",
                8,
                ["rules.example"]));
        var page = await provider.FetchAsync(results.Single().Url);

        Assert.Equal("Fetched Rules", page.Title);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(
            "https://api.firecrawl.test/v2/search",
            handler.Requests[0].Uri);
        Assert.Equal(
            "https://api.firecrawl.test/v2/scrape",
            handler.Requests[1].Uri);
        Assert.Equal("Bearer", handler.Requests[0].AuthorizationScheme);
        Assert.Equal("fc-secret", handler.Requests[0].AuthorizationParameter);

        using var searchJson = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(
            "rules.example",
            searchJson.RootElement.GetProperty("includeDomains")[0].GetString());
        using var scrapeJson = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal(
            "markdown",
            scrapeJson.RootElement.GetProperty("formats")[0].GetString());
        Assert.True(
            scrapeJson.RootElement.GetProperty("onlyMainContent").GetBoolean());
    }

    [Fact]
    public async Task SearchAsync_RequiresConfiguredCredential()
    {
        using var httpClient = new HttpClient(new QueueHandler());
        var provider = new FirecrawlCorpusDiscoveryProvider(
            httpClient,
            new FirecrawlOptions("https://api.firecrawl.test", null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.SearchAsync(
                new CorpusSearchRequest("query", 5, [])));

        Assert.Contains("FIRECRAWL_API_KEY", exception.Message);
    }

    private static HttpResponseMessage CreateResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public QueueHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(
                new RecordedRequest(
                    request.RequestUri!.ToString(),
                    request.Headers.Authorization?.Scheme,
                    request.Headers.Authorization?.Parameter,
                    await request.Content!.ReadAsStringAsync(cancellationToken)));
            return _responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(
        string Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string Body);
}
