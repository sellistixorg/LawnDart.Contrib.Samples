using System.Text.Json;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.WebApp.Services;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class OrderStatusMessageCodecTests
{
    [Fact]
    public void Storefront_records_use_the_ordering_event_names()
    {
        Assert.Equal("order-shipped", MessageTypeNames.Require(typeof(OrderShipped)));
        Assert.Equal("order-shipped", MessageTypeNames.Require(typeof(OrderShippedMessage)));
        Assert.Equal("order-cancelled", MessageTypeNames.Require(typeof(OrderCancelled)));
        Assert.Equal("order-cancelled", MessageTypeNames.Require(typeof(OrderCancelledMessage)));
        Assert.Equal("order-cancelled-for-stock", MessageTypeNames.Require(typeof(OrderCancelledForStock)));
        Assert.Equal("order-cancelled-for-stock", MessageTypeNames.Require(typeof(OrderCancelledForStockMessage)));
    }

    [Fact]
    public void Package_json_reads_ordering_bodies_into_the_smaller_records()
    {
        var id = Guid.Parse("0c9f9b94-a67a-4163-8496-1d1298ab1e02");
        var orderId = Guid.Parse("69a9b215-75fe-4450-8d59-7b236176d97c");
        var timestamp = new DateTime(2026, 10, 9, 1, 33, 48, DateTimeKind.Utc);
        var productId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var shipped = Read<OrderShippedMessage>(
            new OrderShipped(id, timestamp, orderId, "Shipped", "The order was shipped."));
        var cancelled = Read<OrderCancelledMessage>(
            new OrderCancelled(id, timestamp, orderId, "Cancelled", "Changed my mind"));
        var stock = Read<OrderCancelledForStockMessage>(
            new OrderCancelledForStock(id, timestamp, orderId, [productId], "Cancelled"));

        Assert.Equal(id, shipped.Id);
        Assert.Equal(timestamp, shipped.Timestamp);
        Assert.Equal(orderId, shipped.OrderId);
        Assert.Equal(id, cancelled.Id);
        Assert.Equal(timestamp, cancelled.Timestamp);
        Assert.Equal(orderId, cancelled.OrderId);
        Assert.Equal(id, stock.Id);
        Assert.Equal(timestamp, stock.Timestamp);
        Assert.Equal(orderId, stock.OrderId);
    }

    private static TMessage Read<TMessage>(object source)
        where TMessage : class
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(source, source.GetType(), MessagingJson.Options);
        return JsonSerializer.Deserialize<TMessage>(body, MessagingJson.Options)
            ?? throw new InvalidOperationException($"Body did not deserialize to {typeof(TMessage).Name}.");
    }
}
