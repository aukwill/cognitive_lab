namespace CognitiveRuntime.Core.Contracts;

public sealed record ModelRequest(
    string RunId,
    string ModeName,
    string PhaseName,
    PhaseKind PhaseKind,
    string Prompt,
    string Input,
    string? PreviousOutput);

public sealed record ModelResponse(
    string Content,
    string Provider,
    string? Model = null);

public sealed record GitHubModelsOptions(
    string Endpoint,
    string? Token,
    string? Model);

public sealed record AzureFoundryOptions(
    string? Endpoint,
    string? ApiKey,
    string? Deployment,
    string? ApiVersion);
