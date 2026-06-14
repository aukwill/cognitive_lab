using DocumentDistiller.Core.Ingestion;

namespace DocumentDistiller.Tests;

public sealed class DocumentIngestorTests
{
    [Fact]
    public async Task IngestAsync_AssignsStableDocumentAndChunkIds()
    {
        using var workspace = new TestWorkspace();
        workspace.AddDocument(
            "b.md",
            "# Beta\n\nSecond paragraph about evidence.");
        workspace.AddDocument(
            "a.txt",
            "Alpha source.\n\nEvaluation closes the loop.");

        var result = await new DocumentIngestor().IngestAsync(
            workspace.InputRoot,
            maxInputCharacters: 10_000,
            chunkSizeCharacters: 200,
            chunkOverlapCharacters: 20);

        Assert.Equal(["D001", "D002"], result.Documents.Select(document => document.Id));
        Assert.Equal(
            ["a.txt", "b.md"],
            result.Documents.Select(document => document.RelativePath));
        Assert.Equal("Beta", result.Documents[1].Title);
        Assert.All(result.Chunks, chunk => Assert.StartsWith(chunk.SourceId, chunk.Id));
        Assert.All(result.Chunks, chunk => Assert.True(chunk.EndCharacter > chunk.StartCharacter));
        Assert.All(result.Chunks, chunk => Assert.Equal(64, chunk.Sha256.Length));
        Assert.All(result.Documents, document => Assert.Equal(64, document.Sha256.Length));
    }

    [Fact]
    public async Task IngestAsync_RejectsCorpusAboveConfiguredLimit()
    {
        using var workspace = new TestWorkspace();
        workspace.AddDocument("large.txt", new string('x', 500));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DocumentIngestor().IngestAsync(
                workspace.InputRoot,
                maxInputCharacters: 100,
                chunkSizeCharacters: 200,
                chunkOverlapCharacters: 20));

        Assert.Contains("exceeding", exception.Message);
    }

    [Fact]
    public async Task IngestAsync_RecordsExactOverlappingSourceSpans()
    {
        using var workspace = new TestWorkspace();
        workspace.AddDocument(
            "long.txt",
            string.Join(" ", Enumerable.Repeat("evidence runtime evaluation", 30)));

        var result = await new DocumentIngestor().IngestAsync(
            workspace.InputRoot,
            maxInputCharacters: 10_000,
            chunkSizeCharacters: 200,
            chunkOverlapCharacters: 40);

        Assert.True(result.Chunks.Count > 1);
        var first = result.Chunks[0];
        var second = result.Chunks[1];
        Assert.Equal(first.EndCharacter - 40, second.StartCharacter);
        Assert.Equal(
            result.Documents[0].Content[first.StartCharacter..first.EndCharacter],
            first.Content);
        Assert.Equal(
            result.Documents[0].Content[second.StartCharacter..second.EndCharacter],
            second.Content);
    }
}
