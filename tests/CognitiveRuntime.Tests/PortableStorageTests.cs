using System.Security.Cryptography;
using System.Text;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Persistence;
using CognitiveRuntime.Core.Runtime;

namespace CognitiveRuntime.Tests;

public sealed class PortableStorageTests
{
    [Fact]
    public async Task InMemoryRunStateStore_IgnoresOlderRunGeneration()
    {
        var store = new InMemoryRunStateStore();
        var terminal = CreateEntry(
            generation: 6,
            RunLifecycleStatus.Succeeded);
        var stale = CreateEntry(
            generation: 2,
            RunLifecycleStatus.Running);

        await store.UpsertRunAsync(terminal);
        await store.UpsertRunAsync(stale);

        var stored = await store.GetRunAsync(terminal.RunId);

        Assert.NotNull(stored);
        Assert.Equal(6, stored.Generation);
        Assert.Equal(RunLifecycleStatus.Succeeded, stored.LifecycleStatus);
    }

    [Fact]
    public async Task DirectoryArtifactStore_RoundTripsJsonAndHtml()
    {
        using var workspace = new TestWorkspace();
        var store = new DirectoryArtifactStore(
            Path.Combine(workspace.Root, "artifact-store"));
        var json = CreateArtifact(
            "trace.json",
            "application/json; charset=utf-8",
            """{"events":[{"type":"run.finalized"}]}""");
        var html = CreateArtifact(
            "index.html",
            "text/html; charset=utf-8",
            "<!doctype html><title>Run</title>");

        await store.PutAsync(json);
        await store.PutAsync(html);

        var storedJson = await store.GetAsync("run-001", "trace.json");
        var storedHtml = await store.GetAsync("run-001", "index.html");
        var descriptors = await store.ListAsync("run-001");

        Assert.NotNull(storedJson);
        Assert.Equal(json.Content, storedJson.Content);
        Assert.Equal(json.Sha256, storedJson.Sha256);
        Assert.NotNull(storedHtml);
        Assert.Equal(html.Content, storedHtml.Content);
        Assert.Equal(
            ["index.html", "trace.json"],
            descriptors.Select(item => item.RelativePath).ToArray());
    }

    [Fact]
    public async Task PortableStorageTransfer_CopiesSeparateStateAndArtifacts()
    {
        using var workspace = new TestWorkspace();
        var sourceState = new InMemoryRunStateStore();
        var sourceArtifacts = new DirectoryArtifactStore(
            Path.Combine(workspace.Root, "source-artifacts"));
        var destinationState = new InMemoryRunStateStore();
        var destinationArtifacts = new InMemoryArtifactStore();
        var run = CreateEntry(
            generation: 6,
            RunLifecycleStatus.Succeeded);
        var json = CreateArtifact(
            "trace.json",
            "application/json; charset=utf-8",
            """{"events":[{"type":"run.finalized"}]}""");
        var html = CreateArtifact(
            "index.html",
            "text/html; charset=utf-8",
            "<!doctype html><title>Run</title>");

        await sourceState.UpsertRunAsync(run);
        await sourceArtifacts.PutAsync(json);
        await sourceArtifacts.PutAsync(html);

        var transfer = await PortableStorageTransfer.CopyAsync(
            sourceState,
            sourceArtifacts,
            destinationState,
            destinationArtifacts);
        var transferredRun = await destinationState.GetRunAsync(run.RunId);
        var transferredHtml = await destinationArtifacts.GetAsync(
            run.RunId,
            "index.html");

        Assert.Equal(1, transfer.RunCount);
        Assert.Equal(2, transfer.ArtifactCount);
        Assert.Equal(run.RunId, transferredRun?.RunId);
        Assert.Equal(run.Generation, transferredRun?.Generation);
        Assert.Equal(run.LifecycleStatus, transferredRun?.LifecycleStatus);
        Assert.Equal(run.Payload.EvalPassed, transferredRun?.Payload.EvalPassed);
        Assert.Equal(html.Content, transferredHtml?.Content);
    }

    private static RunCatalogEntry CreateEntry(
        long generation,
        RunLifecycleStatus status) =>
        new(
            SchemaVersion: 1,
            Generation: generation,
            RunId: "run-001",
            ModeName: "frame",
            PatternName: "critic-revision",
            ModelProvider: "mock",
            OutputDirectory: "outputs/run-001",
            LifecycleStatus: status,
            Outcome: status == RunLifecycleStatus.Succeeded
                ? RunOutcome.Success
                : null,
            CreatedAt: new DateTimeOffset(
                2026,
                6,
                14,
                12,
                0,
                0,
                TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(
                2026,
                6,
                14,
                12,
                1,
                0,
                TimeSpan.Zero),
            Payload: new RunCatalogPayload(
                Lens: null,
                PipelineStages: [],
                ExecutionNodes: [],
                EvalPassed: status == RunLifecycleStatus.Succeeded));

    private static StoredRunArtifact CreateArtifact(
        string relativePath,
        string mediaType,
        string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new StoredRunArtifact(
            SchemaVersion: 1,
            RunId: "run-001",
            RelativePath: relativePath,
            MediaType: mediaType,
            Content: bytes,
            ByteLength: bytes.LongLength,
            Sha256: Convert.ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant(),
            UpdatedAt: new DateTimeOffset(
                2026,
                6,
                14,
                12,
                1,
                0,
                TimeSpan.Zero));
    }
}
