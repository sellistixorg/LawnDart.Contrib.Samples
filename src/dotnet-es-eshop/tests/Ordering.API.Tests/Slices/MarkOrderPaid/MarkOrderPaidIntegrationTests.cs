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
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.MarkOrderPaid;

public class MarkOrderPaidIntegrationTests
{
    [Fact]
    public async Task Order_paid_is_published_with_the_units_to_deduct()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed),
            typeof(OrderPaid));
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var place = new PlaceOrderCommand(
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
            [new OrderLine(productId, "Alpine boot", 120m, 3)]);

        var order = await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);
        await ctx.AggregateRepository.HandleCommandAsync(order, place);
        await ctx.AggregateRepository.HandleCommandAsync(
            order,
            new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));
        await ctx.AggregateRepository.HandleCommandAsync(
            order,
            new MarkOrderStockConfirmedCommand(Guid.NewGuid(), orderId));
        await ctx.AggregateRepository.HandleCommandAsync(
            order,
            new MarkOrderPaidCommand(Guid.NewGuid(), orderId));

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.Equal(5, recorded.Count);
        Assert.IsType<OrderPlaced>(recorded[0].Event);
        Assert.IsType<OrderStarted>(recorded[1].Event);
        Assert.IsType<OrderAwaitingValidation>(recorded[2].Event);
        Assert.IsType<OrderStatusStockConfirmed>(recorded[3].Event);
        var paid = Assert.IsType<OrderPaid>(recorded[4].Event);
        Assert.Equal(orderId, paid.OrderId);
        Assert.Equal(buyerId, paid.BuyerId);
        var stock = Assert.Single(paid.OrderStockItems);
        Assert.Equal(productId, stock.ProductId);
        Assert.Equal(3, stock.Units);
        Assert.Equal(OrderAggregate.PaidStatus, order.State.OrderStatus);
        Assert.Equal(OrderAggregate.PaidDescription, order.State.Description);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryMessaging();
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IMessageTransport>();
        var seen = new TaskCompletionSource<OrderPaid>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await transport.SubscribeAsync<OrderPaid>((message, _, _) =>
        {
            seen.TrySetResult(message);
            return Task.CompletedTask;
        });

        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderPaid)]);
        var payload = JsonSerializer.SerializeToUtf8Bytes(paid);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = paid.Id,
            EventType = "order-paid",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = payload,
            Metadata = "{}",
            CreatedAt = paid.Timestamp,
            StreamId = $"Order:{orderId}",
            SequencePosition = 5
        });

        var delivered = await seen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(orderId, delivered.OrderId);
        Assert.Equal(buyerId, delivered.BuyerId);
        var deliveredStock = Assert.Single(delivered.OrderStockItems);
        Assert.Equal(productId, deliveredStock.ProductId);
        Assert.Equal(3, deliveredStock.Units);
        var json = JsonSerializer.Serialize(delivered);
        Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alpine boot", json, StringComparison.Ordinal);
    }
}
