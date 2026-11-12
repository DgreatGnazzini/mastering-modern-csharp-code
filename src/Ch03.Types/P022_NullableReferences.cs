using System.Diagnostics.CodeAnalysis;

namespace MasteringModernCSharp.Ch03;

// Email is optional; Id and Name never are. The type says so.
public sealed record Subscriber(string Id, string Name, string? Email);

public sealed class SubscriberDirectory
{
    private readonly Dictionary<string, Subscriber> _byId = [];

    public void Add(Subscriber subscriber)
    {
        // Annotations are compile-time only: public APIs still
        // check what callers with nullable warnings off pass in.
        ArgumentNullException.ThrowIfNull(subscriber);
        _byId[subscriber.Id] = subscriber;
    }

    // "?" on the return type: callers must handle "not found".
    public Subscriber? Find(string id) => _byId.GetValueOrDefault(id);

    // When this returns true, subscriber is not null.
    public bool TryFind(
        string id, [NotNullWhen(true)] out Subscriber? subscriber) =>
        _byId.TryGetValue(id, out subscriber);

    // Non-null fallback in, non-null result out.
    [return: NotNullIfNotNull(nameof(fallback))]
    public string? EmailOf(string id, string? fallback) =>
        Find(id)?.Email ?? fallback;
}

public sealed class Newsletter
{
    private string? _sender;

    public bool IsConfigured => _sender is not null;

    // After this runs, the compiler knows _sender is set.
    [MemberNotNull(nameof(_sender))]
    public void Configure(string sender) => _sender = sender;

    public string Compose(Subscriber subscriber)
    {
        if (_sender is null)
        {
            throw new InvalidOperationException("Call Configure first.");
        }
        // Flow analysis: inside this branch, email is a string.
        if (subscriber.Email is not { Length: > 0 } email)
        {
            return $"(no email for {subscriber.Name})";
        }
        return $"From: {_sender}\nTo: {email}\nHi {subscriber.Name}!";
    }
}
