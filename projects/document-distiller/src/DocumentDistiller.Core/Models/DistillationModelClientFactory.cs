using DocumentDistiller.Core.Abstractions;

namespace DocumentDistiller.Core.Models;

public sealed class DistillationModelClientFactory : IDistillationModelClientFactory
{
    private readonly IReadOnlyDictionary<string, IDistillationModelClient> _clients;

    public DistillationModelClientFactory(
        IEnumerable<IDistillationModelClient> clients)
    {
        _clients = clients.ToDictionary(
            client => client.ProviderName,
            StringComparer.OrdinalIgnoreCase);
    }

    public IDistillationModelClient Resolve(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        return _clients.TryGetValue(providerName, out var client)
            ? client
            : throw new InvalidOperationException(
                $"Unknown model provider '{providerName}'. Available providers: " +
                $"{string.Join(", ", _clients.Keys.OrderBy(key => key))}.");
    }
}
