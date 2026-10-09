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
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderStockConfirmed;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentSucceeded;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenPaymentSucceeded;

public class WhenPaymentSucceededRegistrationTests
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
                    == typeof(EShopReactorHost<WhenPaymentSucceededReactor, PaymentSucceeded>))
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
        var productId = Guid.NewGuid();
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
            [new OrderLine(productId, "Alpine boot", 120m, 2)]));
        await new MarkOrderAwaitingValidationHandler(orders).HandleAsync(
            new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));
        await new MarkOrderStockConfirmedHandler(orders).HandleAsync(
            new MarkOrderStockConfirmedCommand(Guid.NewGuid(), orderId));

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (transport.SubscriberCount<PaymentSucceeded>() == 0)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Payment Succeeded subscription did not start.");
                await Task.Delay(20);
            }

            var succeeded = new PaymentSucceeded(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                orderId,
                "gw-42",
                240m);
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, succeeded, messageId);
            await PublishAsync(transport, succeeded, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            var paid = Assert.IsType<OrderPaid>(
                Assert.Single(recorded, row => row.Event is OrderPaid).Event);
            Assert.Equal(orderId, paid.OrderId);
            Assert.Equal(buyerId, paid.BuyerId);
            var line = Assert.Single(paid.OrderStockItems);
            Assert.Equal(productId, line.ProductId);
            Assert.Equal(2, line.Units);
            Assert.Equal(
                OrderAggregate.PaidStatus,
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
        context.AddOrderingContext(typeof(OrderPlaced).Assembly, typeof(PaymentSucceeded).Assembly);
        services.AddEShopReactors(typeof(WhenPaymentSucceededReactor).Assembly);
    }

    private static async Task PublishAsync(
        IMessageTransport transport,
        PaymentSucceeded succeeded,
        Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(PaymentSucceeded)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "payment-succeeded",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(succeeded),
            Metadata = "{}",
            CreatedAt = succeeded.Timestamp,
            StreamId = $"PaymentAttempt:{succeeded.AttemptId:D}",
            SequencePosition = 1
        });
    }
}
