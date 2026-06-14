using System.Data.Common;
using CognitiveRuntime.Cli;
using CognitiveRuntime.Core.Persistence;
using Microsoft.Extensions.Configuration;

namespace CognitiveRuntime.Tests;

public sealed class PersistenceFactoryTests
{
    [Fact]
    public void RunStateFactory_DefaultsToDisabled()
    {
        var configuration = new ConfigurationBuilder().Build();

        var store = DatabaseRunStateStoreFactory.Create(configuration);

        Assert.IsType<NullRunStateStore>(store);
    }

    [Fact]
    public void RunStateFactory_CreatesConfiguredRelationalAdapter()
    {
        var configuration = BuildConfiguration(
            ("COGNITIVE_RUNTIME_RUN_STATE_PROVIDER", "postgresql"),
            ("COGNITIVE_RUNTIME_RUN_STATE_CONNECTION_STRING", "test"),
            (
                "COGNITIVE_RUNTIME_RUN_STATE_FACTORY_TYPE",
                typeof(TestDbProviderFactory).AssemblyQualifiedName!));

        var store = DatabaseRunStateStoreFactory.Create(configuration);

        Assert.IsType<AdoNetRunStateStore>(store);
    }

    [Fact]
    public void RunStateFactory_AcceptsLegacyDatabaseAliases()
    {
        var configuration = BuildConfiguration(
            ("COGNITIVE_RUNTIME_DATABASE_PROVIDER", "sqlserver"),
            ("COGNITIVE_RUNTIME_DATABASE_CONNECTION_STRING", "test"),
            (
                "COGNITIVE_RUNTIME_DATABASE_FACTORY_TYPE",
                typeof(TestDbProviderFactory).AssemblyQualifiedName!));

        var store = DatabaseRunStateStoreFactory.Create(configuration);

        Assert.IsType<AdoNetRunStateStore>(store);
    }

    [Fact]
    public void ArtifactFactory_CreatesDirectoryAdapter()
    {
        using var workspace = new TestWorkspace();
        var configuration = BuildConfiguration(
            ("COGNITIVE_RUNTIME_ARTIFACT_PROVIDER", "directory"),
            (
                "COGNITIVE_RUNTIME_ARTIFACT_ROOT",
                Path.Combine(workspace.Root, "objects")));

        var store = ArtifactStoreFactory.Create(
            workspace.OutputRoot,
            configuration);

        Assert.IsType<DirectoryArtifactStore>(store);
    }

    private static IConfiguration BuildConfiguration(
        params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.ToDictionary(
                    item => item.Key,
                    item => (string?)item.Value,
                    StringComparer.Ordinal))
            .Build();

    public sealed class TestDbProviderFactory : DbProviderFactory
    {
        public static readonly TestDbProviderFactory Instance = new();
    }
}
