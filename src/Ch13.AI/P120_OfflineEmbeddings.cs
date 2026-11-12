using Microsoft.Extensions.AI;

namespace MasteringModernCSharp.Ch13;

// A stand-in for tests and offline demos: hashes each word into a
// bucket. Texts that share words score high; it has no notion of
// meaning, so "car" and "automobile" don't match.
public sealed class HashingEmbeddingGenerator(int dimensions)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
            values.Select(v => new Embedding<float>(Embed(v)))));

    private float[] Embed(string text)
    {
        var vector = new float[dimensions];
        var words = text.ToLowerInvariant().Split(
            [' ', '\n', '.', ',', '?', '!'],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            vector[Fnv1a(word) % (uint)dimensions] += 1;
        }
        var length = MathF.Sqrt(vector.Sum(x => x * x));
        return length == 0 ? vector : [.. vector.Select(x => x / length)];
    }

    // Stable across runs, unlike string.GetHashCode().
    private static uint Fnv1a(string word)
    {
        var hash = 2166136261;
        foreach (var c in word)
        {
            hash = (hash ^ c) * 16777619;
        }
        return hash;
    }

    public object? GetService(Type serviceType, object? key = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
