using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace MasteringModernCSharp.Ch10.Tests;

// Records what was published, and can fail the first few calls.
public sealed class FakeBus : IMessageBus
{
    public int FailuresLeft { get; set; }
    public ConcurrentQueue<(Guid Id, string Type, string Payload)>
        Published { get; } = new();

    public Task PublishAsync(Guid messageId, string type,
        string payload, CancellationToken ct)
    {
        if (FailuresLeft-- > 0)
            throw new HttpRequestException("broker unavailable");
        Published.Enqueue((messageId, type, payload));
        return Task.CompletedTask;
    }
}

public class P097_OutboxTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));

    private async Task<(TestDb Test, int Customer)> SetupAsync()
    {
        var test = await TestDb.CreateAsync();
        await using var db = test.Context();
        var customer = TestDb.NewCustomer("Ada", "London", false);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(TestDb.Ct);
        return (test, customer.Id);
    }

    private OutboxRelay Relay(ShopDb db, FakeBus bus) =>
        new(db, bus, _clock, NullLogger<OutboxRelay>.Instance);

    [Fact]
    public async Task Order_and_message_are_saved_together()
    {
        var (test, customer) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();

        var id = await Ordering.PlaceAsync(db, customer, _clock,
                                           TestDb.Ct);

        var message = await db.Outbox.SingleAsync(TestDb.Ct);
        Assert.Equal(nameof(OrderPlaced), message.Type);
        Assert.Null(message.PublishedAt);
        var payload = JsonSerializer.Deserialize<OrderPlaced>(
            message.Payload);
        Assert.Equal(new OrderPlaced(id, customer), payload);
    }

    [Fact]
    public async Task Failed_order_leaves_no_message()
    {
        var (test, _) = await SetupAsync();
        await using var __ = test;
        await using var db = test.Context();

        // No such customer: the foreign key rejects the order.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Ordering.PlaceAsync(db, 999, _clock, TestDb.Ct));

        await using var check = test.Context();
        Assert.Equal(0, await check.Outbox.CountAsync(TestDb.Ct));
    }

    #region p097-relay-test
    [Fact]
    public async Task Relay_retries_until_the_broker_accepts()
    {
        var (test, customer) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();
        await Ordering.PlaceAsync(db, customer, _clock, TestDb.Ct);
        var bus = new FakeBus { FailuresLeft = 1 };

        Assert.Equal(0, await Relay(db, bus)
            .PublishPendingAsync(ct: TestDb.Ct));     // broker down
        Assert.Equal(1, await Relay(db, bus)
            .PublishPendingAsync(ct: TestDb.Ct));     // next pass
        Assert.Equal(0, await Relay(db, bus)
            .PublishPendingAsync(ct: TestDb.Ct));     // nothing left

        var message = await db.Outbox.SingleAsync(TestDb.Ct);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime,
                     message.PublishedAt);
        Assert.Single(bus.Published);
    }
    #endregion

    [Fact]
    public async Task Relay_gives_up_after_max_attempts()
    {
        var (test, customer) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();
        await Ordering.PlaceAsync(db, customer, _clock, TestDb.Ct);
        var bus = new FakeBus { FailuresLeft = int.MaxValue };

        for (var i = 0; i < OutboxRelay.MaxAttempts + 2; i++)
            await Relay(db, bus).PublishPendingAsync(ct: TestDb.Ct);

        var message = await db.Outbox.SingleAsync(TestDb.Ct);
        Assert.Equal(OutboxRelay.MaxAttempts, message.Attempts);
        Assert.Equal("broker unavailable", message.LastError);
    }

    #region p097-consumer-test
    [Fact]
    public async Task Duplicate_delivery_creates_one_shipment()
    {
        var (test, customer) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();
        var orderId = await Ordering.PlaceAsync(db, customer, _clock,
                                                TestDb.Ct);
        var messageId = Guid.CreateVersion7();
        var message = new OrderPlaced(orderId, customer);

        var first = await new CreateShipment(db, _clock)
            .HandleAsync(messageId, message, TestDb.Ct);
        var second = await new CreateShipment(test.Context(), _clock)
            .HandleAsync(messageId, message, TestDb.Ct);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await db.Shipments.CountAsync(TestDb.Ct));
    }
    #endregion

    [Fact]
    public async Task Racing_duplicate_is_detected_by_the_key()
    {
        var (test, customer) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();
        var orderId = await Ordering.PlaceAsync(db, customer, _clock,
                                                TestDb.Ct);
        var messageId = Guid.CreateVersion7();

        // The other copy commits after this one checked, but before
        // it saved.
        await using var other = test.Context();
        other.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = nameof(CreateShipment),
        });
        db.SavingChanges += (_, _) =>
        {
            if (other.ChangeTracker.HasChanges()) other.SaveChanges();
        };

        var handled = await new CreateShipment(db, _clock).HandleAsync(
            messageId, new OrderPlaced(orderId, customer), TestDb.Ct);

        Assert.False(handled);
        await using var check = test.Context();
        Assert.Equal(0, await check.Shipments.CountAsync(TestDb.Ct));
    }

    [Fact]
    public async Task Worker_publishes_in_the_background()
    {
        var (test, customer) = await SetupAsync();
        await using var _ = test;
        await using (var db = test.Context())
            await Ordering.PlaceAsync(db, customer, _clock, TestDb.Ct);

        var bus = new FakeBus();
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped(_ => test.Context())
            .AddSingleton<IMessageBus>(bus)
            .AddOutbox()
            .AddSingleton<TimeProvider>(_clock)   // replaces System
            .BuildServiceProvider();
        var worker = services.GetServices<IHostedService>().Single();

        await worker.StartAsync(TestDb.Ct);
        for (var i = 0; i < 100 && bus.Published.IsEmpty; i++)
            await Task.Delay(20, TestDb.Ct);
        await worker.StopAsync(TestDb.Ct);

        Assert.Single(bus.Published);
    }
}
