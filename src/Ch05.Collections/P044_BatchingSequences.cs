namespace MasteringModernCSharp.Ch05;

public static class Batching
{
    // Streams lines lazily and hands them out 500 at a time,
    // so memory stays flat however large the file is.
    public static IEnumerable<string[]> ReadInBatches(
        string path, int batchSize = 500) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Chunk(batchSize);

    public static async Task<int> ImportAsync(
        string path, Func<string[], Task> saveBatch)
    {
        var batches = 0;
        foreach (var batch in ReadInBatches(path))
        {
            await saveBatch(batch);
            batches++;
        }
        return batches;
    }
}
