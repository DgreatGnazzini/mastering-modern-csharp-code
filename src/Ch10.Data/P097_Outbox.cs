using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MasteringModernCSharp.Ch10;

#region p097-schema
// A message waiting to be published, saved with the business data.
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Type { get; set; }
    public required string Payload { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public static OutboxMessage From<T>(T message, DateTime now) => new()
    {
        Type = typeof(T).Name,
        Payload = JsonSerializer.Serialize(message),
        OccurredAt = now,
    };
}

// The messages each consumer has already handled.
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public required string Consumer { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
}

public record OrderPlaced(int OrderId, int CustomerId);

public partial class ShopDb
{
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages =>
        Set<ProcessedMessage>();
    public DbSet<Shipment> Shipments => Set<Shipment>();

    private void ConfigureMessaging(ModelBuilder model)
    {
        model.Entity<OutboxMessage>(m =>
        {
            m.ToTable("Outbox");
            m.HasIndex(x => new { x.PublishedAt, x.OccurredAt });
        });
        // The key makes a second insert for the same message fail.
        model.Entity<ProcessedMessage>()
            .HasKey(p => new { p.MessageId, p.Consumer });
    }
}
#endregion

#region p097-producer
public static class Ordering
{
    // The order and its OrderPlaced message commit together.
    public static async Task<int> PlaceAsync(
        ShopDb db, int customerId, TimeProvider clock,
        CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var order = new Order
        {
            TenantId = db.TenantId, CustomerId = customerId,
            PlacedAt = now,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);          // assigns order.Id

        db.Outbox.Add(OutboxMessage.From(
            new OrderPlaced(order.Id, customerId), now));
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return order.Id;
    }
}
#endregion

#region p097-relay
public interface IMessageBus
{
    Task PublishAsync(Guid messageId, string type, string payload,
                      CancellationToken ct);
}

// Copies unpublished messages from the outbox to the broker.
public sealed class OutboxRelay(
    ShopDb db, IMessageBus bus, TimeProvider clock,
    ILogger<OutboxRelay> log)
{
    public const int MaxAttempts = 10;

    public async Task<int> PublishPendingAsync(
        int batchSize = 20, CancellationToken ct = default)
    {
        var batch = await db.Outbox
            .Where(m => m.PublishedAt == null
                        && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(ct);

        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                await bus.PublishAsync(message.Id, message.Type,
                                       message.Payload, ct);
                message.PublishedAt = clock.GetUtcNow().UtcDateTime;
                published++;
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                log.LogWarning(ex, "Publishing {Id} failed", message.Id);
            }
            // Record each result at once, so a crash repeats at most
            // one message.
            await db.SaveChangesAsync(ct);
        }
        return published;
    }
}

public sealed class OutboxWorker(
    IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<OutboxWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(2), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var relay = scope.ServiceProvider
                    .GetRequiredService<OutboxRelay>();
                while (await relay.PublishPendingAsync(ct: stop) > 0) { }
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Outbox relay failed");  // try again
            }
        }
        while (await timer.WaitForNextTickAsync(stop));
    }
}

public static class OutboxServices
{
    public static IServiceCollection AddOutbox(
        this IServiceCollection services) => services
        .AddSingleton(TimeProvider.System)
        .AddScoped<OutboxRelay>()
        .AddHostedService<OutboxWorker>();
}
#endregion

#region p097-consumer
// Brokers deliver at least once, so the same message can arrive
// twice. The consumer records each message it handles in the same
// transaction as its work.
public sealed class CreateShipment(ShopDb db, TimeProvider clock)
{
    private const string Name = nameof(CreateShipment);

    public async Task<bool> HandleAsync(
        Guid messageId, OrderPlaced message, CancellationToken ct)
    {
        if (await SeenAsync(messageId, ct)) return false;

        db.Shipments.Add(new Shipment { OrderId = message.OrderId });
        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = Name,
            ProcessedAt = clock.GetUtcNow().UtcDateTime,
        });
        try
        {
            await db.SaveChangesAsync(ct);   // both rows, or neither
            return true;
        }
        catch (DbUpdateException)
        {
            // Another copy of the message got there first.
            db.ChangeTracker.Clear();
            if (await SeenAsync(messageId, ct)) return false;
            throw;
        }
    }

    private Task<bool> SeenAsync(Guid id, CancellationToken ct) =>
        db.ProcessedMessages.AnyAsync(
            p => p.MessageId == id && p.Consumer == Name, ct);
}
#endregion
