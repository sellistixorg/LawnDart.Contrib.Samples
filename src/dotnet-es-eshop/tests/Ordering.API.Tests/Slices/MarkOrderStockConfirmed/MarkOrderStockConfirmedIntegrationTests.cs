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

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.MarkOrderStockConfirmed;

public class MarkOrderStockConfirmedIntegrationTests
{
    [Fact]
    public async Task Order_status_stock_confirmed_is_published_with_the_charge_amount()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed));
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

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.Equal(4, recorded.Count);
        Assert.IsType<OrderPlaced>(recorded[0].Event);
        Assert.IsType<OrderStarted>(recorded[1].Event);
        Assert.IsType<OrderAwaitingValidation>(recorded[2].Event);
        var confirmed = Assert.IsType<OrderStatusStockConfirmed>(recorded[3].Event);
        Assert.Equal(orderId, confirmed.OrderId);
        Assert.Equal(buyerId, confirmed.BuyerId);
        Assert.Equal("Ada Lovelace", confirmed.BuyerName);
        Assert.Equal(360m, confirmed.OrderTotal);
        Assert.Equal(OrderAggregate.StockConfirmedStatus, order.State.OrderStatus);
        Assert.Equal(OrderAggregate.StockConfirmedDescription, order.State.Description);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryMessaging();
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IMessageTransport>();
        var seen = new TaskCompletionSource<OrderStatusStockConfirmed>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await transport.SubscribeAsync<OrderStatusStockConfirmed>((message, _, _) =>
        {
            seen.TrySetResult(message);
            return Task.CompletedTask;
        });

        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderStatusStockConfirmed)]);
        var payload = JsonSerializer.SerializeToUtf8Bytes(confirmed);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = confirmed.Id,
            EventType = "order-status-stock-confirmed",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = payload,
            Metadata = "{}",
            CreatedAt = confirmed.Timestamp,
            StreamId = $"Order:{orderId}",
            SequencePosition = 4
        });

        var delivered = await seen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(orderId, delivered.OrderId);
        Assert.Equal(buyerId, delivered.BuyerId);
        Assert.Equal("Ada Lovelace", delivered.BuyerName);
        Assert.Equal(360m, delivered.OrderTotal);
        var json = JsonSerializer.Serialize(delivered);
        Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alpine boot", json, StringComparison.Ordinal);
    }
}
