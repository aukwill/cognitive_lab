using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Persistence;
using Microsoft.Extensions.Configuration;

namespace CognitiveRuntime.Cli;

internal static class DatabaseRunStateStoreFactory
{
    private const string ProviderKey =
        "COGNITIVE_RUNTIME_RUN_STATE_PROVIDER";
    private const string ConnectionStringKey =
        "COGNITIVE_RUNTIME_RUN_STATE_CONNECTION_STRING";
    private const string FactoryTypeKey =
        "COGNITIVE_RUNTIME_RUN_STATE_FACTORY_TYPE";
    private const string LegacyProviderKey =
        "COGNITIVE_RUNTIME_DATABASE_PROVIDER";
    private const string LegacyConnectionStringKey =
        "COGNITIVE_RUNTIME_DATABASE_CONNECTION_STRING";
    private const string LegacyFactoryTypeKey =
        "COGNITIVE_RUNTIME_DATABASE_FACTORY_TYPE";

    public static IRunStateStore Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = GetConfiguredValue(
            configuration,
            ProviderKey,
            LegacyProviderKey);
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(
                provider,
                "disabled",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                provider,
                "none",
                StringComparison.OrdinalIgnoreCase))
        {
            return new NullRunStateStore();
        }

        var dialect = provider.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" =>
                RelationalDatabaseDialect.PostgreSql,
            "sqlserver" =>
                RelationalDatabaseDialect.SqlServer,
            _ => throw new InvalidOperationException(
                $"Unknown run-state provider '{provider}'. Supported values: " +
                "disabled, postgresql, sqlserver.")
        };
        var configuredConnectionString = GetConfiguredValue(
            configuration,
            ConnectionStringKey,
            LegacyConnectionStringKey);
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringKey} is required for run-state provider " +
                $"'{provider}'.");
        }

        var factoryType = GetConfiguredValue(
            configuration,
            FactoryTypeKey,
            LegacyFactoryTypeKey);
        if (string.IsNullOrWhiteSpace(factoryType))
        {
            throw new InvalidOperationException(
                $"{FactoryTypeKey} is required for run-state provider " +
                $"'{provider}'.");
        }

        return new AdoNetRunStateStore(
            DbProviderFactoryResolver.Resolve(factoryType),
            new AdoNetRunStateStoreOptions(
                configuredConnectionString,
                dialect));
    }

    private static string? GetConfiguredValue(
        IConfiguration configuration,
        string key,
        string fallbackKey)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? configuration[fallbackKey]?.Trim()
            : value;
    }
}
