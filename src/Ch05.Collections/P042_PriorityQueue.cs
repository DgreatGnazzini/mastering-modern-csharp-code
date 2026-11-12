namespace MasteringModernCSharp.Ch05;

public enum Severity { Critical = 0, High = 1, Normal = 2, Low = 3 }

public record Ticket(string Title, Severity Severity, DateTime Opened);

public sealed class TicketQueue
{
    // Lower priority values come out first; ties go to older tickets.
    private readonly PriorityQueue<Ticket, (Severity, DateTime)> _queue =
        new();

    public int Count => _queue.Count;

    public void Add(Ticket t) => _queue.Enqueue(t, (t.Severity, t.Opened));

    public bool TryTakeNext(out Ticket? ticket) =>
        _queue.TryDequeue(out ticket, out _);

    public Ticket? PeekNext() =>
        _queue.TryPeek(out var ticket, out _) ? ticket : null;
}
