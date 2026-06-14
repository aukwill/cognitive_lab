using System.Text.Json;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.Discovery;

namespace DocumentDistiller.Tests;

public sealed class CorpusFunnelTests
{
    [Fact]
    public async Task BuildAsync_AppliesPolicyAndWritesProvenanceManifest()
    {
        using var workspace = new TestWorkspace();
        var provider = new FakeDiscoveryProvider(
            [
                new CorpusSearchResult(
                    1,
                    "First Source",
                    "https://docs.example.com/one",
                    "First"),
                new CorpusSearchResult(
                    2,
                    "Duplicate",
                    "https://docs.example.com/one#section",
                    "Duplicate"),
                new CorpusSearchResult(
                    3,
                    "Domain Overflow",
                    "https://docs.example.com/two",
                    "Overflow"),
                new CorpusSearchResult(
                    4,
                    "Not Allowed",
                    "https://other.example.net/three",
                    "Other")
            ],
            new Dictionary<string, CorpusPage>
            {
                ["https://docs.example.com/one"] = new(
                    "Fetched Title",
                    "https://docs.example.com/one",
                    new string('x', 1_100))
            });
        var funnel = new CorpusFunnel(
            provider,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 6, 14, 1, 2, 3, TimeSpan.Zero)));

        var result = await funnel.BuildAsync(
            new CorpusFunnelRequest(
                "meaningful danger",
                workspace.OutputRoot,
                ["example.com"],
                MaxSources: 3,
                MaxSourceCharacters: 1_000,
                MaxSourcesPerDomain: 1));

        Assert.Equal(1, result.SourceCount);
        var sourcePath = Directory.GetFiles(result.CorpusDirectory).Single();
        var source = await File.ReadAllTextAsync(sourcePath);
        Assert.Contains("# Fetched Title", source);
        Assert.Contains("Source truncated by corpus funnel policy.", source);

        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.ManifestPath));
        var candidates = manifest.RootElement.GetProperty("candidates");
        Assert.Equal(4, candidates.GetArrayLength());
        Assert.True(candidates[0].GetProperty("selected").GetBoolean());
        Assert.True(candidates[0].GetProperty("truncated").GetBoolean());
        Assert.Equal(
            64,
            candidates[0].GetProperty("sha256").GetString()!.Length);
        Assert.Equal(
            "Duplicate URL.",
            candidates[1].GetProperty("rejectionReason").GetString());
        Assert.Equal(
            "Per-domain source limit reached.",
            candidates[2].GetProperty("rejectionReason").GetString());
        Assert.Equal(
            "Domain is not allowlisted.",
            candidates[3].GetProperty("rejectionReason").GetString());
    }

    private sealed class FakeDiscoveryProvider : ICorpusDiscoveryProvider
    {
        private readonly IReadOnlyList<CorpusSearchResult> _results;
        private readonly IReadOnlyDictionary<string, CorpusPage> _pages;

        public FakeDiscoveryProvider(
            IReadOnlyList<CorpusSearchResult> results,
            IReadOnlyDictionary<string, CorpusPage> pages)
        {
            _results = results;
            _pages = pages;
        }

        public string ProviderName => "fake";

        public Task<IReadOnlyList<CorpusSearchResult>> SearchAsync(
            CorpusSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results);

        public Task<CorpusPage> FetchAsync(
            string url,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_pages[url]);
    }
}
