using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Exceptions;

namespace CognitiveRuntime.Core.Models;

public sealed class ModelClientFactory : IModelClientFactory
{
    private readonly IReadOnlyDictionary<string, IModelClient> _clients;

    public ModelClientFactory(IEnumerable<IModelClient> clients)
    {
        _clients = clients.ToDictionary(
            client => client.ProviderName,
            StringComparer.OrdinalIgnoreCase);
    }

    public IModelClient Resolve(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var canonicalName = providerName.Trim().ToLowerInvariant() switch
        {
            "github" => "github-models",
            "azure" => "azure-foundry",
            _ => providerName.Trim()
        };

        if (_clients.TryGetValue(canonicalName, out var client))
        {
            return client;
        }

        var available = string.Join(", ", _clients.Keys.Order());
        throw new ModelProviderException(
            $"Unknown model provider '{providerName}'. Available providers: {available}.");
    }
}
