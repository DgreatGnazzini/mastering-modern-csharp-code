using Microsoft.Extensions.AI;

namespace MasteringModernCSharp.Ch13.Tests;

#region p118-fake
// Replays scripted replies and records what it was sent.
// Tests run offline, for free, and give the same answer every time.
public sealed class FakeChatClient(params ChatMessage[] replies)
    : IChatClient
{
    private readonly Queue<ChatMessage> _replies = new(replies);
    public List<List<ChatMessage>> Requests { get; } = [];
    public List<ChatOptions?> Options { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        Options.Add(options);
        return Task.FromResult(new ChatResponse(_replies.Dequeue())
        {
            ModelId = "fake-model",
        });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? key = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
#endregion
