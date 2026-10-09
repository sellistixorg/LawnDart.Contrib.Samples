using System.Text.Json;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenCatalogStockRejected;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenCatalogStockRejected;

public class WhenCatalogStockRejectedRegistrationTests
{
    [Fact]
    public void Reactor_is_registered_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        RegisterOrdering(services);

        var reactors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<WhenCatalogStockRejectedReactor, OrderStockRejected>))
            .ToArray();

        Assert.Single(reactors);
    }

    [Fact]
    public async Task Redelivery_of_the_same_message_id_does_not_write_twice()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterOrdering(builder.Services);

        using var host = builder.Build();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        var orders = host.Services.GetRequiredService<IAggregateRepository>();
        await new PlaceOrderHandler(orders).HandleAsync(new PlaceOrderCommand(
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
            [
                new OrderLine(bootId, "Alpine boot", 120m, 2),
                new OrderLine(capId, "Wool cap", 25m, 1)
            ]));
        await new MarkOrderAwaitingValidationHandler(orders).HandleAsync(
            new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (transport.SubscriberCount<OrderStockRejected>() == 0)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Order Stock Rejected subscription did not start.");
                await Task.Delay(20);
            }

            var rejected = new OrderStockRejected(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [
                    new ConfirmedOrderStockItem(bootId, true),
                    new ConfirmedOrderStockItem(capId, false)
                ]);
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, rejected, messageId);
            await PublishAsync(transport, rejected, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            var cancelled = Assert.IsType<OrderCancelledForStock>(
                Assert.Single(recorded, row => row.Event is OrderCancelledForStock).Event);
            Assert.Equal(orderId, cancelled.OrderId);
            Assert.Equal([capId], cancelled.RejectedProductIds);
            Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);

            var json = JsonSerializer.Serialize(cancelled);
            Assert.Contains("RejectedProductIds", json, StringComparison.Ordinal);
            Assert.Contains(capId.ToString("D"), json, StringComparison.Ordinal);
            Assert.DoesNotContain(bootId.ToString("D"), json, StringComparison.Ordinal);
            Assert.DoesNotContain("HasStock", json, StringComparison.Ordinal);
            Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("unitPrice", json, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(
                OrderAggregate.CancelledStatus,
                (await orders.GetOrCreateAsync<OrderAggregate>(orderId)).State.OrderStatus);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void RegisterOrdering(IServiceCollection services)
    {
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddOrderingContext(typeof(OrderPlaced).Assembly, typeof(OrderStockRejected).Assembly);
        services.AddEShopReactors(typeof(WhenCatalogStockRejectedReactor).Assembly);
    }

    private static async Task PublishAsync(
        IMessageTransport transport,
        OrderStockRejected rejected,
        Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderStockRejected)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "order-stock-rejected",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(rejected),
            Metadata = "{}",
            CreatedAt = rejected.Timestamp,
            StreamId = $"OrderStockValidation:{rejected.OrderId:D}",
            SequencePosition = 1
        });
    }
}
