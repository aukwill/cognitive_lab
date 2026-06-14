namespace DocumentDistiller.Core.Contracts;

public sealed record CorpusSearchRequest(
    string Query,
    int Limit,
    IReadOnlyList<string> IncludeDomains);

public sealed record CorpusSearchResult(
    int Rank,
    string Title,
    string Url,
    string Description);

public sealed record CorpusPage(
    string Title,
    string Url,
    string Markdown);

public sealed record CorpusFunnelRequest(
    string Query,
    string OutputRoot,
    IReadOnlyList<string> IncludeDomains,
    int MaxSources = 8,
    int MaxSourceCharacters = 30_000,
    int MaxSourcesPerDomain = 2);

public sealed record CorpusCandidate(
    int Rank,
    string Title,
    string Url,
    string Domain,
    string Description,
    bool Selected,
    string? RejectionReason,
    string? LocalPath,
    int ContentCharacters,
    bool Truncated,
    string? Sha256);

public sealed record CorpusDiscoveryManifest(
    int SchemaVersion,
    string DiscoveryId,
    DateTimeOffset CreatedAt,
    string Provider,
    string Query,
    IReadOnlyList<string> IncludeDomains,
    int MaxSources,
    int MaxSourceCharacters,
    int MaxSourcesPerDomain,
    IReadOnlyList<CorpusCandidate> Candidates);

public sealed record CorpusFunnelResult(
    string DiscoveryId,
    string DiscoveryDirectory,
    string CorpusDirectory,
    string ManifestPath,
    int SourceCount);

public sealed record FirecrawlOptions(
    string Endpoint,
    string? ApiKey,
    string ApiKeyEnvironmentVariable = "FIRECRAWL_API_KEY");
