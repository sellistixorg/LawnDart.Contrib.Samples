using System.Text.Json;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.PlaceOrder;

public class PlaceOrderIntegrationTests
{
    [Fact]
    public async Task Order_started_is_published_with_the_buyer_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(OrderPlaced), typeof(OrderStarted));
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var command = new PlaceOrderCommand(
            orderId,
            orderId,
            buyerId,
            "Ada Lovelace",
            "1 Main",
            "Seattle",
            "WA",
            "USA",
            "98101",
            1,
            "Ada Lovelace",
            "4444",
            new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            [new OrderLine(Guid.NewGuid(), "Alpine boot", 120m, 2)]);

        var order = await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);
        await ctx.AggregateRepository.HandleCommandAsync(order, command);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var started = Assert.IsType<OrderStarted>(
            Assert.Single(recorded, row => row.Event is OrderStarted).Event);
        Assert.Equal(buyerId, started.BuyerId);
        Assert.Equal(orderId, started.OrderId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryMessaging();
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IMessageTransport>();
        var seen = new TaskCompletionSource<OrderStarted>(TaskCreationOptions.RunContinuationsAsynchronously);
        await transport.SubscribeAsync<OrderStarted>((message, _, _) =>
        {
            seen.TrySetResult(message);
            return Task.CompletedTask;
        });

        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderStarted)]);
        var payload = JsonSerializer.SerializeToUtf8Bytes(started);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = started.Id,
            EventType = "order-started",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = payload,
            Metadata = "{}",
            CreatedAt = started.Timestamp,
            StreamId = $"Order:{orderId}",
            SequencePosition = 2
        });

        var delivered = await seen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(buyerId, delivered.BuyerId);
        Assert.Equal(orderId, delivered.OrderId);
        Assert.DoesNotContain("Card", JsonSerializer.Serialize(delivered), StringComparison.Ordinal);
    }
}
