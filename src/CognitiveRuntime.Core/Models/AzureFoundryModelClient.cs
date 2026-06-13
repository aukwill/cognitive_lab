using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Models;

public sealed class AzureFoundryModelClient : IModelClient
{
    private readonly AzureFoundryOptions _options;

    public AzureFoundryModelClient(AzureFoundryOptions options)
    {
        _options = options;
    }

    public string ProviderName => "azure-foundry";

    public Task<ModelResponse> CompleteAsync(
        ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missingSettings = new[]
            {
                (Name: "AZURE_FOUNDRY_ENDPOINT", Value: _options.Endpoint),
                (Name: "AZURE_FOUNDRY_API_KEY", Value: _options.ApiKey),
                (Name: "AZURE_FOUNDRY_DEPLOYMENT", Value: _options.Deployment),
                (Name: "AZURE_FOUNDRY_API_VERSION", Value: _options.ApiVersion)
            }
            .Where(setting => string.IsNullOrWhiteSpace(setting.Value))
            .Select(setting => setting.Name)
            .ToArray();

        var detail = missingSettings.Length == 0
            ? "Configuration was found."
            : $"Missing configuration: {string.Join(", ", missingSettings)}.";

        throw new ModelProviderException(
            $"The Azure Foundry adapter is an intentional MVP stub. {detail}");
    }
}
