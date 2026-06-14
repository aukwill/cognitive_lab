using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentDistiller.Core.Abstractions;
using DocumentDistiller.Core.Contracts;
using DocumentDistiller.Core.IO;
using DocumentDistiller.Core.Serialization;

namespace DocumentDistiller.Core.Discovery;

public sealed partial class CorpusFunnel : ICorpusFunnel
{
    private readonly ICorpusDiscoveryProvider _provider;
    private readonly TimeProvider _timeProvider;

    public CorpusFunnel(
        ICorpusDiscoveryProvider provider,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _timeProvider = timeProvider;
    }

    public async Task<CorpusFunnelResult> BuildAsync(
        CorpusFunnelRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var discoveryId = Guid.NewGuid().ToString("N");
        var timestamp = _timeProvider
            .GetUtcNow()
            .ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var discoveryDirectory = Path.GetFullPath(
            Path.Combine(
                request.OutputRoot,
                $"{timestamp}_discovery_{discoveryId[..8]}"));
        var corpusDirectory = Path.Combine(discoveryDirectory, "corpus");
        var manifestPath = Path.Combine(
            discoveryDirectory,
            "discovery_manifest.json");
        Directory.CreateDirectory(corpusDirectory);

        var searchResults = await _provider.SearchAsync(
            new CorpusSearchRequest(
                request.Query,
                Math.Min(40, Math.Max(request.MaxSources * 4, request.MaxSources)),
                NormalizeDomains(request.IncludeDomains)),
            cancellationToken);

        var candidates = new List<CorpusCandidate>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var domainCounts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var selectedCount = 0;

        foreach (var result in searchResults.OrderBy(item => item.Rank))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = Evaluate(
                result,
                request,
                seenUrls,
                domainCounts,
                selectedCount);
            if (!decision.Selected)
            {
                candidates.Add(decision);
                continue;
            }

            CorpusPage page;
            try
            {
                page = await _provider.FetchAsync(result.Url, cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                candidates.Add(
                    decision with
                    {
                        Selected = false,
                        RejectionReason = $"Fetch failed: {exception.Message}"
                    });
                continue;
            }

            if (string.IsNullOrWhiteSpace(page.Markdown))
            {
                candidates.Add(
                    decision with
                    {
                        Selected = false,
                        RejectionReason = "Provider returned empty content."
                    });
                continue;
            }

            var truncated = page.Markdown.Length > request.MaxSourceCharacters;
            var markdown = truncated
                ? page.Markdown[..request.MaxSourceCharacters] +
                    "\n\n[Source truncated by corpus funnel policy.]\n"
                : page.Markdown;
            var sourceTitle = string.IsNullOrWhiteSpace(page.Title)
                ? result.Title
                : page.Title;
            var fileName =
                $"{selectedCount + 1:D2}-{Slugify(sourceTitle)}.md";
            var localPath = Path.Combine(corpusDirectory, fileName);
            var content = RenderSource(
                sourceTitle,
                page.Url,
                request.Query,
                markdown);
            await FilePersistence.WriteAllTextAtomicAsync(
                localPath,
                content,
                cancellationToken);

            selectedCount++;
            seenUrls.Add(NormalizeUrl(result.Url));
            domainCounts[decision.Domain] =
                domainCounts.GetValueOrDefault(decision.Domain) + 1;
            candidates.Add(
                decision with
                {
                    Title = sourceTitle,
                    LocalPath = $"corpus/{fileName}",
                    ContentCharacters = content.Length,
                    Truncated = truncated,
                    Sha256 = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(content)))
                });
        }

        if (selectedCount == 0)
        {
            throw new InvalidOperationException(
                "Corpus discovery did not produce any usable sources.");
        }

        var manifest = new CorpusDiscoveryManifest(
            1,
            discoveryId,
            _timeProvider.GetUtcNow(),
            _provider.ProviderName,
            request.Query,
            NormalizeDomains(request.IncludeDomains),
            request.MaxSources,
            request.MaxSourceCharacters,
            request.MaxSourcesPerDomain,
            candidates);
        await FilePersistence.WriteAllTextAtomicAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, JsonDefaults.Options),
            cancellationToken);

        return new CorpusFunnelResult(
            discoveryId,
            discoveryDirectory,
            corpusDirectory,
            manifestPath,
            selectedCount);
    }

    private static CorpusCandidate Evaluate(
        CorpusSearchResult result,
        CorpusFunnelRequest request,
        HashSet<string> seenUrls,
        Dictionary<string, int> domainCounts,
        int selectedCount)
    {
        if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return Rejected(result, string.Empty, "Only HTTPS sources are allowed.");
        }

        var domain = uri.IdnHost.ToLowerInvariant();
        var normalizedUrl = NormalizeUrl(result.Url);
        if (!seenUrls.Add(normalizedUrl))
        {
            return Rejected(result, domain, "Duplicate URL.");
        }

        seenUrls.Remove(normalizedUrl);
        var includeDomains = NormalizeDomains(request.IncludeDomains);
        if (includeDomains.Count > 0 &&
            !includeDomains.Any(
                allowed => domain.Equals(
                        allowed,
                        StringComparison.OrdinalIgnoreCase) ||
                    domain.EndsWith(
                        $".{allowed}",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return Rejected(result, domain, "Domain is not allowlisted.");
        }

        if (selectedCount >= request.MaxSources)
        {
            return Rejected(result, domain, "Source limit reached.");
        }

        if (domainCounts.GetValueOrDefault(domain) >=
            request.MaxSourcesPerDomain)
        {
            return Rejected(result, domain, "Per-domain source limit reached.");
        }

        return new CorpusCandidate(
            result.Rank,
            result.Title,
            result.Url,
            domain,
            result.Description,
            true,
            null,
            null,
            0,
            false,
            null);
    }

    private static CorpusCandidate Rejected(
        CorpusSearchResult result,
        string domain,
        string reason) =>
        new(
            result.Rank,
            result.Title,
            result.Url,
            domain,
            result.Description,
            false,
            reason,
            null,
            0,
            false,
            null);

    private static string RenderSource(
        string title,
        string url,
        string query,
        string markdown) =>
        $"""
        # {title}

        - Source URL: {url}
        - Discovery query: {query}
        - Retrieved as provider-generated Markdown.

        ---

        {markdown.Trim()}
        """;

    private static string NormalizeUrl(string url)
    {
        var builder = new UriBuilder(url)
        {
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static IReadOnlyList<string> NormalizeDomains(
        IReadOnlyList<string> domains) =>
        domains
            .Select(
                domain => domain
                    .Trim()
                    .ToLowerInvariant()
                    .TrimStart('.'))
            .Where(domain => domain.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Slugify(string title)
    {
        var value = NonSlugCharacters()
            .Replace(title.ToLowerInvariant(), "-")
            .Trim('-');
        if (value.Length == 0)
        {
            return "source";
        }

        return value.Length <= 60 ? value : value[..60].TrimEnd('-');
    }

    private static void Validate(CorpusFunnelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MaxSources, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.MaxSources, 20);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            request.MaxSourceCharacters,
            1_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            request.MaxSourcesPerDomain,
            1);
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugCharacters();
}
