using System.Text.Json;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.RabbitMQ;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderPaid;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderStockConfirmed;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentFailed;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenPaymentFailed;

public class WhenPaymentFailedRegistrationTests
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
                    == typeof(EShopReactorHost<WhenPaymentFailedReactor, PaymentFailed>))
            .ToArray();

        Assert.Single(reactors);
        Assert.Equal("payment-failed", MessageTypeNames.Require(typeof(PaymentFailed)));
        Assert.Equal(
            EShopServiceNames.Ordering,
            RabbitMqNames.Queue(
                EShopServiceNames.Ordering,
                RabbitMqQueueLayout.Subscription,
                MessageTypeNames.Require(typeof(PaymentFailed))));
    }

    [Fact]
    public async Task Redelivery_of_the_same_message_id_does_not_write_twice()
    {
        var logs = new CollectingLoggerProvider();
        using var host = await StartAsync(logs);
        try
        {
            var orderId = Guid.NewGuid();
            var buyerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            await StockConfirmedAsync(host.Services, orderId, buyerId, productId);

            var transport = Transport(host);
            var failed = Failed(orderId, "Card declined");
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, failed, messageId);
            await PublishAsync(transport, failed, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            var cancelled = Assert.IsType<OrderCancelled>(
                Assert.Single(recorded, row => row.Event is OrderCancelled).Event);
            Assert.Equal(orderId, cancelled.OrderId);
            Assert.Equal("Card declined", cancelled.Reason);
            Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);

            var json = JsonSerializer.Serialize(cancelled);
            Assert.Contains("Card declined", json, StringComparison.Ordinal);
            Assert.DoesNotContain("4444", json, StringComparison.Ordinal);
            Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);

            var order = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<OrderAggregate>(orderId);
            Assert.Equal(OrderAggregate.CancelledStatus, order.State.OrderStatus);
            Assert.Equal(buyerId, order.State.BuyerId);
            Assert.DoesNotContain(logs.Warnings, message => message.Contains("Card declined", StringComparison.Ordinal));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Paid_order_is_logged_and_a_second_delivery_does_not_run_again()
    {
        var logs = new CollectingLoggerProvider();
        using var host = await StartAsync(logs);
        try
        {
            var orderId = Guid.NewGuid();
            var buyerId = Guid.NewGuid();
            await StockConfirmedAsync(host.Services, orderId, buyerId, Guid.NewGuid());
            var orders = host.Services.GetRequiredService<IAggregateRepository>();
            await new MarkOrderPaidHandler(orders).HandleAsync(new MarkOrderPaidCommand(Guid.NewGuid(), orderId));

            var transport = Transport(host);
            var failed = Failed(orderId, "Card declined");
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, failed, messageId);
            await PublishAsync(transport, failed, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            Assert.DoesNotContain(recorded, row => row.Event is OrderCancelled);
            var order = await orders.GetOrCreateAsync<OrderAggregate>(orderId);
            Assert.Equal(OrderAggregate.PaidStatus, order.State.OrderStatus);
            Assert.Equal(buyerId, order.State.BuyerId);

            var message = Assert.Single(logs.Warnings);
            Assert.Contains(failed.Id.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(OrderAggregate.CannotCancelOrderTitle, message, StringComparison.Ordinal);
            Assert.Contains("Skipping", message, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Unknown_order_is_logged_and_a_second_delivery_does_not_run_again()
    {
        var logs = new CollectingLoggerProvider();
        using var host = await StartAsync(logs);
        try
        {
            var orderId = Guid.NewGuid();
            var transport = Transport(host);
            var failed = Failed(orderId, "Card declined");
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, failed, messageId);
            await PublishAsync(transport, failed, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            Assert.DoesNotContain(recorded, row => row.Event is OrderCancelled);

            var message = Assert.Single(logs.Warnings);
            Assert.Contains(failed.Id.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(OrderAggregate.OrderNotFoundTitle, message, StringComparison.Ordinal);
            Assert.Contains("Skipping", message, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task<IHost> StartAsync(CollectingLoggerProvider logs)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        RegisterOrdering(builder.Services);

        var host = builder.Build();
        await host.StartAsync();
        var transport = Transport(host);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<PaymentFailed>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Payment Failed subscription did not start.");
            await Task.Delay(20);
        }

        return host;
    }

    private static void RegisterOrdering(IServiceCollection services)
    {
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddOrderingContext(typeof(OrderPlaced).Assembly, typeof(PaymentFailed).Assembly);
        services.AddEShopReactors(typeof(WhenPaymentFailedReactor).Assembly);
    }

    private static InMemoryMessageTransport Transport(IHost host) =>
        (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();

    private static async Task StockConfirmedAsync(
        IServiceProvider services,
        Guid orderId,
        Guid buyerId,
        Guid productId)
    {
        var orders = services.GetRequiredService<IAggregateRepository>();
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
    }

    private static async Task PublishAsync(
        IMessageTransport transport,
        PaymentFailed failed,
        Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(PaymentFailed)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "payment-failed",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(failed),
            Metadata = "{}",
            CreatedAt = failed.Timestamp,
            StreamId = $"PaymentAttempt:{failed.AttemptId:D}",
            SequencePosition = 1
        });
    }

    private static PaymentFailed Failed(Guid orderId, string? reason) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), orderId, reason);

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public List<string> Warnings { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(Warnings);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger : ILogger
        {
            private readonly List<string> _warnings;

            public CollectingLogger(List<string> warnings) => _warnings = warnings;

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                    _warnings.Add(formatter(state, exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
