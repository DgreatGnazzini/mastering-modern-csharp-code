using System.Threading.Channels;

namespace MasteringModernCSharp.Ch07;

public static class ImagePipeline
{
    // One producer feeds a bounded channel; several workers drain
    // it. When the channel is full, the producer waits, so memory
    // stays flat no matter how fast items arrive.
    public static async Task<List<string>> RunAsync(
        IAsyncEnumerable<string> files,
        Func<string, CancellationToken, Task<string>> process,
        int workers = 4,
        int capacity = 16,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
            });
        var results = new List<string>();
        var resultsLock = new Lock();

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var file in files
                    .WithCancellation(cancellationToken))
                {
                    await channel.Writer.WriteAsync(
                        file, cancellationToken);
                }
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        }, cancellationToken);

        var consumers = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var file in channel.Reader
                    .ReadAllAsync(cancellationToken))
                {
                    var output = await process(file, cancellationToken);
                    lock (resultsLock) results.Add(output);
                }
            }, cancellationToken));

        await Task.WhenAll([producer, .. consumers]);
        return results;
    }
}
