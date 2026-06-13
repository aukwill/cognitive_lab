using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Exceptions;
using CognitiveRuntime.Core.Models;

namespace CognitiveRuntime.Tests;

public sealed class ModelClientFactoryTests
{
    [Theory]
    [InlineData("mock", "mock")]
    [InlineData("github", "github-models")]
    [InlineData("github-models", "github-models")]
    [InlineData("azure", "azure-foundry")]
    public void Resolve_UsesCanonicalProviderAliases(
        string requested,
        string expected)
    {
        var factory = new ModelClientFactory(
            [
                new StubModelClient("mock"),
                new StubModelClient("github-models"),
                new StubModelClient("azure-foundry")
            ]);

        var client = factory.Resolve(requested);

        Assert.Equal(expected, client.ProviderName);
    }

    [Fact]
    public void Resolve_ReportsAvailableProvidersForUnknownName()
    {
        var factory = new ModelClientFactory([new StubModelClient("mock")]);

        var exception = Assert.Throws<ModelProviderException>(
            () => factory.Resolve("missing"));

        Assert.Contains("mock", exception.Message);
        Assert.Contains("missing", exception.Message);
    }

    private sealed class StubModelClient(string providerName) : IModelClient
    {
        public string ProviderName { get; } = providerName;

        public Task<ModelResponse> CompleteAsync(
            ModelRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelResponse("content", ProviderName));
    }
}
