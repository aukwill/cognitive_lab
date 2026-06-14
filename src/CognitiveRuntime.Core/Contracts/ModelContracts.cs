namespace CognitiveRuntime.Core.Contracts;

public sealed record ModelRequest(
    string RunId,
    string ModeName,
    string PhaseName,
    PhaseKind PhaseKind,
    string Prompt,
    string Input,
    IReadOnlyList<PhaseResult> PriorPhaseResults);

public sealed record ModelResponse(
    string Content,
    string Provider,
    string? Model = null);

public sealed record GitHubModelsOptions(
    string Endpoint,
    string? Token,
    string? Model,
    string ApiVersion = "2026-03-10");

public sealed record AzureFoundryOptions(
    string? Endpoint,
    string? ApiKey,
    string? Deployment,
    string? ApiVersion);

public sealed record OpenRouterOptions(
    string Endpoint,
    string? ApiKey,
    string? Model,
    bool EnableCodeExecution = false);
