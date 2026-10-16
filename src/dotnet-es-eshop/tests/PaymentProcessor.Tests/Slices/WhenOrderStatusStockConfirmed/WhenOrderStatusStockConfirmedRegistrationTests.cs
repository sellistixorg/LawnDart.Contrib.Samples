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
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using Microsoft.Extensions.Options;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.WhenOrderStatusStockConfirmed;

public class WhenOrderStatusStockConfirmedRegistrationTests
{
    [Fact]
    public void Reactor_is_registered_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        RegisterPayment(services);

        var reactors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<WhenOrderStatusStockConfirmedReactor, OrderStatusStockConfirmed>))
            .ToArray();

        Assert.Single(reactors);
        Assert.Equal(
            "order-status-stock-confirmed",
            MessageTypeNames.Require(typeof(OrderStatusStockConfirmed)));
        Assert.NotEqual("order-stock-confirmed", MessageTypeNames.Require(typeof(OrderStatusStockConfirmed)));
        Assert.Equal(
            EShopServiceNames.PaymentProcessor,
            RabbitMqNames.Queue(
                EShopServiceNames.PaymentProcessor,
                RabbitMqQueueLayout.Subscription,
                MessageTypeNames.Require(typeof(OrderStatusStockConfirmed))));
    }

    [Fact]
    public async Task Redelivery_of_the_same_message_id_does_not_write_twice()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterPayment(builder.Services);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscriberAsync(transport);

            var orderId = Guid.NewGuid();
            var buyerId = Guid.NewGuid();
            var confirmed = Confirmed(orderId, buyerId, 27.15m);
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, confirmed, messageId);
            await PublishAsync(transport, confirmed, messageId);

            var attemptId = AttemptId(messageId.ToString("D"));
            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"PaymentAttempt:{attemptId}");
            var requested = Assert.IsType<PaymentChargeRequested>(Assert.Single(recorded).Event);
            Assert.Equal(attemptId, requested.AttemptId);
            Assert.Equal(orderId, requested.OrderId);
            Assert.Equal(27.15m, requested.Amount);
            Assert.Null(requested.Currency);
            Assert.Equal(PaymentAttempt.RequestedStatus, requested.Status);
            var attempts = host.Services.GetRequiredService<IAggregateRepository>();
            var attempt = await attempts.GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(1, attempt.Version);
            Assert.Equal(orderId, attempt.State.OrderId);
            Assert.Equal(buyerId, Assert.Single(host.Services.GetRequiredService<RecordingGateway>().Requests).BuyerId);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Rejected_amount_is_not_retried()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterPayment(builder.Services);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscriberAsync(transport);

            var orderId = Guid.NewGuid();
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, Confirmed(orderId, Guid.NewGuid(), 0m), messageId);
            await PublishAsync(transport, Confirmed(orderId, Guid.NewGuid(), 0m), messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            Assert.Empty(await store.ReadStreamAsync($"PaymentAttempt:{AttemptId(messageId.ToString("D"))}"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void RegisterPayment(IServiceCollection services)
    {
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        services.AddOptions<PaymentOptions>();
        services.AddSingleton<RecordingGateway>();
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<RecordingGateway>());
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddPaymentContext(
            typeof(WhenOrderStatusStockConfirmedReactor).Assembly,
            typeof(OrderStatusStockConfirmed).Assembly);
        services.AddEShopReactors(typeof(WhenOrderStatusStockConfirmedReactor).Assembly);
    }

    private static async Task WaitForSubscriberAsync(InMemoryMessageTransport transport)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<OrderStatusStockConfirmed>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Order Status Stock Confirmed subscription did not start.");
            await Task.Delay(20);
        }
    }

    private static async Task PublishAsync(
        IMessageTransport transport,
        OrderStatusStockConfirmed confirmed,
        Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderStatusStockConfirmed)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "order-status-stock-confirmed",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(confirmed),
            Metadata = "{}",
            CreatedAt = confirmed.Timestamp,
            StreamId = $"Order:{confirmed.OrderId:D}",
            SequencePosition = 1
        });
    }

    private static OrderStatusStockConfirmed Confirmed(Guid orderId, Guid buyerId, decimal orderTotal) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, buyerId, orderTotal, "Ada Lovelace");

    private static Guid AttemptId(string messageId) =>
        EShopVersion5Id.ForPrefixedName("payment-attempt", messageId);

    private sealed class RecordingGateway : IPaymentGateway
    {
        private readonly SimulatedPaymentGateway _inner;

        public RecordingGateway(IMessageTransport transport)
        {
            _inner = new SimulatedPaymentGateway(
                Options.Create(new PaymentOptions { PaymentSucceeded = true }),
                transport);
        }

        public List<PaymentChargeRequest> Requests { get; } = [];

        public async Task<PaymentGatewayResult> ChargeAsync(
            PaymentChargeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return await _inner.ChargeAsync(request, cancellationToken);
        }
    }
}
