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
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenCatalogStockConfirmed;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenCatalogStockConfirmed;

public class WhenCatalogStockConfirmedRegistrationTests
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
                    == typeof(EShopReactorHost<WhenCatalogStockConfirmedReactor, OrderStockConfirmed>))
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
            [new OrderLine(Guid.NewGuid(), "Alpine boot", 120m, 2)]));
        await new MarkOrderAwaitingValidationHandler(orders).HandleAsync(
            new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (transport.SubscriberCount<OrderStockConfirmed>() == 0)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Order Stock Confirmed subscription did not start.");
                await Task.Delay(20);
            }

            var confirmed = new OrderStockConfirmed(Guid.NewGuid(), DateTime.UtcNow, orderId);
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, confirmed, messageId);
            await PublishAsync(transport, confirmed, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            var status = Assert.IsType<OrderStatusStockConfirmed>(
                Assert.Single(recorded, row => row.Event is OrderStatusStockConfirmed).Event);
            Assert.Equal(orderId, status.OrderId);
            Assert.Equal(buyerId, status.BuyerId);
            Assert.Equal(
                OrderAggregate.StockConfirmedStatus,
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
        context.AddOrderingContext(typeof(OrderPlaced).Assembly, typeof(OrderStockConfirmed).Assembly);
        services.AddEShopReactors(typeof(WhenCatalogStockConfirmedReactor).Assembly);
    }

    private static async Task PublishAsync(
        IMessageTransport transport,
        OrderStockConfirmed confirmed,
        Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderStockConfirmed)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "order-stock-confirmed",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(confirmed),
            Metadata = "{}",
            CreatedAt = confirmed.Timestamp,
            StreamId = $"OrderStockValidation:{confirmed.OrderId:D}",
            SequencePosition = 1
        });
    }
}
