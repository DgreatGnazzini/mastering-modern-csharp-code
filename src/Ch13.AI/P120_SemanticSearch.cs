using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace MasteringModernCSharp.Ch13;

public sealed class DocChunk
{
    [VectorStoreKey] public required string Id { get; init; }
    [VectorStoreData] public required string Title { get; init; }
    [VectorStoreData] public required string Text { get; init; }

    // A string vector property: the store calls the embedding
    // generator for you, on upsert and on search.
    [VectorStoreVector(Dimensions,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => $"{Title}\n{Text}";

    public const int Dimensions = 1536;  // text-embedding-3-small
}

public sealed class DocSearch(
    IEmbeddingGenerator<string, Embedding<float>> embeddings)
{
    private readonly VectorStoreCollection<string, DocChunk> _docs =
        new InMemoryVectorStore(new() { EmbeddingGenerator = embeddings })
            .GetCollection<string, DocChunk>("docs");

    public async Task IndexAsync(
        IEnumerable<DocChunk> chunks, CancellationToken ct = default)
    {
        await _docs.EnsureCollectionExistsAsync(ct);
        await _docs.UpsertAsync(chunks, ct);
    }

    public async Task<List<(string Title, double Score)>> SearchAsync(
        string query, int top = 3, CancellationToken ct = default)
    {
        var hits = new List<(string, double)>();
        await foreach (var hit in _docs.SearchAsync(
                           query, top, cancellationToken: ct))
        {
            hits.Add((hit.Record.Title, hit.Score ?? 0));
        }
        return hits;
    }
}
