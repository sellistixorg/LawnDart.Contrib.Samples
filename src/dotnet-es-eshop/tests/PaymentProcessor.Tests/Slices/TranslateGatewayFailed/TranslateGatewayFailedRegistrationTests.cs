using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.RabbitMQ;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewayFailed;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.TranslateGatewayFailed;

public class TranslateGatewayFailedRegistrationTests
{
    [Fact]
    public void Subscription_is_registered_once()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterPayment(builder.Services);
        builder.AddEShopSlices(typeof(TranslateGatewayFailedReactor).Assembly);

        var hosted = builder.Services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(TranslateGatewayFailedSubscription))
            .ToArray();

        Assert.Single(hosted);
        Assert.Equal(
            "payment-gateway-charge-failed",
            MessageTypeNames.Require(typeof(PaymentGatewayChargeFailed)));
        Assert.Equal(
            "payment-gateway-charge-failed.payment-processor",
            RabbitMqNames.Queue(
                MessageTypeNames.Require(typeof(PaymentGatewayChargeFailed)),
                EShopServiceNames.PaymentProcessor));
        Assert.DoesNotContain(
            EShopSlices.FindReactors(typeof(TranslateGatewayFailedReactor).Assembly),
            pair => pair.Reactor == typeof(TranslateGatewayFailedReactor));
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<WhenOrderStatusStockConfirmedReactor, OrderStatusStockConfirmed>));
    }

    [Fact]
    public async Task Redelivery_of_the_same_message_id_does_not_write_twice()
    {
        using var host = await StartAsync();
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host.Services, attemptId, orderId, 27.15m, "usd");
            var transport = Transport(host);
            var messageId = PaymentGatewayChargeFailed.MessageIdFor(attemptId);
            await PublishAsync(transport, Failed(attemptId, orderId, " card declined "), messageId);
            await PublishAsync(transport, Failed(attemptId, orderId, "another"), messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.Equal(2, recorded.Count);
            var failed = Assert.IsType<PaymentFailed>(recorded[1].Event);
            Assert.Equal(attemptId, failed.AttemptId);
            Assert.Equal(orderId, failed.OrderId);
            Assert.Equal("card declined", failed.FailureReason);
            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(PaymentAttempt.FailedStatus, attempt.State.Status);
            Assert.Equal(27.15m, attempt.State.Amount);
            Assert.Equal("usd", attempt.State.Currency);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Later_message_id_on_a_failed_attempt_writes_nothing()
    {
        using var host = await StartAsync();
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host.Services, attemptId, orderId, 9m, "USD");
            var transport = Transport(host);
            await PublishAsync(
                transport,
                Failed(attemptId, orderId, "declined"),
                PaymentGatewayChargeFailed.MessageIdFor(attemptId));
            await PublishAsync(
                transport,
                Failed(attemptId, orderId, "again"),
                Guid.NewGuid().ToString("D"));

            var recorded = await host.Services.GetRequiredService<IEventStore>()
                .ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.Equal(2, recorded.Count);
            Assert.Equal("declined", Assert.IsType<PaymentFailed>(recorded[1].Event).FailureReason);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Unknown_attempt_is_not_retried()
    {
        using var host = await StartAsync();
        try
        {
            var attemptId = Guid.NewGuid();
            var transport = Transport(host);
            var messageId = PaymentGatewayChargeFailed.MessageIdFor(attemptId);
            await PublishAsync(transport, Failed(attemptId, Guid.NewGuid(), "declined"), messageId);
            await PublishAsync(transport, Failed(attemptId, Guid.NewGuid(), "declined"), messageId);

            Assert.Empty(await host.Services.GetRequiredService<IEventStore>()
                .ReadStreamAsync($"PaymentAttempt:{attemptId}"));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Mismatched_order_is_not_retried()
    {
        using var host = await StartAsync();
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host.Services, attemptId, orderId, 3m, "USD");
            var transport = Transport(host);
            var messageId = Guid.NewGuid().ToString("D");
            await PublishAsync(transport, Failed(attemptId, Guid.NewGuid(), "declined"), messageId);
            await PublishAsync(transport, Failed(attemptId, Guid.NewGuid(), "declined"), messageId);

            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(1, attempt.Version);
            Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
            Assert.Equal(orderId, attempt.State.OrderId);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        using var host = await StartAsync();
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host.Services, attemptId, orderId, 5m, "USD");
            var transport = Transport(host);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PublishAsync(transport, Failed(attemptId, orderId, "declined"), " "));

            Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(1, attempt.Version);
            Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
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
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddPaymentContext(
            typeof(TranslateGatewayFailedReactor).Assembly,
            typeof(PaymentFailed).Assembly);
    }

    private static async Task<IHost> StartAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterPayment(builder.Services);
        builder.AddEShopSlices(typeof(TranslateGatewayFailedReactor).Assembly);
        var host = builder.Build();
        await host.StartAsync();
        await WaitForSubscriberAsync(Transport(host));
        return host;
    }

    private static InMemoryMessageTransport Transport(IHost host) =>
        (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();

    private static async Task WaitForSubscriberAsync(InMemoryMessageTransport transport)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<PaymentGatewayChargeFailed>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Payment Gateway Charge Failed subscription did not start.");
            await Task.Delay(20);
        }
    }

    private static async Task ChargeAsync(
        IServiceProvider services,
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency)
    {
        var attempts = services.GetRequiredService<IAggregateRepository>();
        var attempt = await attempts.GetOrCreateAsync<PaymentAttempt>(attemptId);
        await attempts.HandleCommandAsync(
            attempt,
            new ChargeOrderPaymentCommand(
                Guid.NewGuid(),
                attemptId,
                orderId,
                amount,
                currency,
                Guid.NewGuid()));
    }

    private static Task PublishAsync(
        IMessageTransport transport,
        PaymentGatewayChargeFailed failed,
        string messageId)
    {
        var context = new MessageContext
        {
            MessageId = messageId,
            CorrelationId = messageId,
            EnqueuedAt = DateTimeOffset.UtcNow
        };
        return transport.PublishAsync(failed, context);
    }

    private static PaymentGatewayChargeFailed Failed(Guid attemptId, Guid orderId, string? reason) =>
        new(attemptId, orderId, 27.15m, "usd", null, reason);
}
