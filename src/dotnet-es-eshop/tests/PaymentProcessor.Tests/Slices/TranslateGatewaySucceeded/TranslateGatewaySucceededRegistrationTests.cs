using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewaySucceeded;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.TranslateGatewaySucceeded;

public class TranslateGatewaySucceededRegistrationTests
{
    [Fact]
    public void Subscriber_is_registered_once()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        var assembly = typeof(TranslateGatewaySucceededMessageHost).Assembly;
        builder.Services.AddEShopReactors(assembly);
        builder.AddEShopSlices(assembly);
        builder.AddEShopSlices(assembly);

        var hosted = builder.Services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(TranslateGatewaySucceededHost))
            .ToArray();

        Assert.Single(hosted);
        Assert.Equal(
            "payment-gateway-charge-succeeded",
            EShopRabbitMq.RoutingKey(typeof(PaymentGatewayChargeSucceeded)));
        Assert.Equal("eshop.payment-processor", EShopRabbitMq.QueueName(EShopServiceNames.PaymentProcessor));
    }

    [Fact]
    public async Task Gateway_success_records_payment_succeeded()
    {
        var warnings = new WarningLog();
        using var host = await StartAsync(warnings);
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host, attemptId, orderId, 42.50m, "USD");

            var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);
            await PublishAsync(
                host,
                Callback(attemptId, orderId, 42.50m, "EUR", "  gw-1  "),
                messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.Equal(2, recorded.Count);
            var succeeded = Assert.IsType<PaymentSucceeded>(recorded[1].Event);
            Assert.Equal(attemptId, succeeded.AttemptId);
            Assert.Equal(orderId, succeeded.OrderId);
            Assert.Equal("gw-1", succeeded.GatewayReference);
            Assert.Equal(42.50m, succeeded.Amount);

            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(PaymentAttempt.SucceededStatus, attempt.State.Status);
            Assert.Equal(orderId, attempt.State.OrderId);
            Assert.Equal(42.50m, attempt.State.Amount);
            Assert.Equal("USD", attempt.State.Currency);
            Assert.Equal("gw-1", attempt.State.GatewayReference);
            Assert.Empty(warnings.Messages);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Same_message_id_records_payment_succeeded_once()
    {
        using var host = await StartAsync(new WarningLog());
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host, attemptId, orderId, 19m, "USD");
            var callback = Callback(attemptId, orderId, 19m, "USD", null);
            var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);

            await PublishAsync(host, callback, messageId);
            await PublishAsync(host, callback, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.Single(recorded, stored => stored.Event is PaymentSucceeded);
            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(2, attempt.Version);
            Assert.Equal(PaymentAttempt.SucceededStatus, attempt.State.Status);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Later_callback_for_a_finished_attempt_writes_nothing()
    {
        using var host = await StartAsync(new WarningLog());
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host, attemptId, orderId, 11m, "USD");
            await PublishAsync(
                host,
                Callback(attemptId, orderId, 11m, "USD", "gw"),
                PaymentGatewayChargeSucceeded.MessageIdFor(attemptId));
            await PublishAsync(
                host,
                Callback(attemptId, orderId, 99m, "USD", "other"),
                Guid.NewGuid().ToString("D"));

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"PaymentAttempt:{attemptId}");
            var succeeded = Assert.IsType<PaymentSucceeded>(Assert.Single(recorded, stored => stored.Event is PaymentSucceeded).Event);
            Assert.Equal("gw", succeeded.GatewayReference);
            Assert.Equal(11m, succeeded.Amount);
            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(PaymentAttempt.SucceededStatus, attempt.State.Status);
            Assert.Equal("gw", attempt.State.GatewayReference);
            Assert.Equal(11m, attempt.State.Amount);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Missing_attempt_is_logged_and_not_retried()
    {
        var warnings = new WarningLog();
        using var host = await StartAsync(warnings);
        try
        {
            var attemptId = Guid.NewGuid();
            var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);
            var callback = Callback(attemptId, Guid.NewGuid(), 5m, null, null);

            await PublishAsync(host, callback, messageId);
            await PublishAsync(host, callback, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            Assert.Empty(await store.ReadStreamAsync($"PaymentAttempt:{attemptId}"));
            var message = Assert.Single(warnings.Messages);
            Assert.Contains(attemptId.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(PaymentAttempt.AttemptNotFoundMessage, message, StringComparison.Ordinal);
            Assert.Contains(PaymentAttempt.AttemptNotFoundTitle, message, StringComparison.Ordinal);
            Assert.Contains("Skipping", message, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Mismatched_order_is_logged_and_not_retried()
    {
        var warnings = new WarningLog();
        using var host = await StartAsync(warnings);
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host, attemptId, orderId, 7m, "USD");
            var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);
            var callback = Callback(attemptId, Guid.NewGuid(), 7m, "USD", null);

            await PublishAsync(host, callback, messageId);
            await PublishAsync(host, callback, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.IsType<PaymentChargeRequested>(Assert.Single(recorded).Event);
            var attempt = await host.Services.GetRequiredService<IAggregateRepository>()
                .GetOrCreateAsync<PaymentAttempt>(attemptId);
            Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
            Assert.Equal(orderId, attempt.State.OrderId);
            var message = Assert.Single(warnings.Messages);
            Assert.Contains(attemptId.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(PaymentAttempt.OrderMismatchMessage, message, StringComparison.Ordinal);
            Assert.Contains(PaymentAttempt.OrderMismatchTitle, message, StringComparison.Ordinal);
            Assert.Contains("Skipping", message, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown_and_a_later_delivery_records()
    {
        var log = new WarningLog();
        using var host = await StartAsync(log);
        try
        {
            var attemptId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            await ChargeAsync(host, attemptId, orderId, 3m, "USD");
            var transport = Transport(host);
            var callback = Callback(attemptId, orderId, 3m, "USD", "gw");

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                transport.PublishAsync(callback, new MessageContext { MessageId = " " }));

            Assert.Contains(attemptId.ToString("D"), error.Message, StringComparison.Ordinal);
            Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
            var logged = Assert.Single(log.Errors);
            Assert.Contains(attemptId.ToString(), logged, StringComparison.Ordinal);
            Assert.Contains("Translate Gateway Succeeded failed", logged, StringComparison.Ordinal);
            var before = await host.Services.GetRequiredService<IEventStore>()
                .ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.DoesNotContain(before, stored => stored.Event is PaymentSucceeded);

            await PublishAsync(host, callback, PaymentGatewayChargeSucceeded.MessageIdFor(attemptId));
            var recorded = await host.Services.GetRequiredService<IEventStore>()
                .ReadStreamAsync($"PaymentAttempt:{attemptId}");
            Assert.Single(recorded, stored => stored.Event is PaymentSucceeded);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task<IHost> StartAsync(WarningLog warnings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(warnings);
        RegisterPayment(builder);

        var host = builder.Build();
        await host.StartAsync();
        var transport = Transport(host);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<PaymentGatewayChargeSucceeded>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Payment gateway charge succeeded subscription did not start.");
            await Task.Delay(20);
        }

        return host;
    }

    private static void RegisterPayment(HostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddPaymentContext(
            typeof(TranslateGatewaySucceededMessageHost).Assembly,
            typeof(PaymentSucceeded).Assembly);
        services.AddEShopReactors(typeof(TranslateGatewaySucceededMessageHost).Assembly);
        builder.AddEShopSlices(typeof(TranslateGatewaySucceededMessageHost).Assembly);
    }

    private static async Task ChargeAsync(
        IHost host,
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string currency)
    {
        var attempts = host.Services.GetRequiredService<IAggregateRepository>();
        var attempt = await attempts.GetOrCreateAsync<PaymentAttempt>(attemptId);
        await attempts.HandleCommandAsync(
            attempt,
            new ChargeOrderPaymentCommand(Guid.NewGuid(), attemptId, orderId, amount, currency, null));
    }

    private static Task PublishAsync(IHost host, PaymentGatewayChargeSucceeded callback, string messageId)
    {
        var transport = Transport(host);
        return transport.PublishAsync(
            callback,
            new MessageContext
            {
                MessageId = messageId,
                CorrelationId = messageId,
                EnqueuedAt = DateTimeOffset.UtcNow
            });
    }

    private static InMemoryMessageTransport Transport(IHost host) =>
        (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();

    private static PaymentGatewayChargeSucceeded Callback(
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency,
        string? gatewayReference) =>
        new(attemptId, orderId, amount, currency, gatewayReference);

    private sealed class WarningLog : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public List<string> Errors { get; } = [];

        public ILogger CreateLogger(string categoryName) => new WarningLogger(categoryName, Messages, Errors);

        public void Dispose()
        {
        }

        private sealed class WarningLogger : ILogger
        {
            private readonly string _category;
            private readonly List<string> _messages;
            private readonly List<string> _errors;

            public WarningLogger(string category, List<string> messages, List<string> errors)
            {
                _category = category;
                _messages = messages;
                _errors = errors;
            }

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
                if (!_category.Contains(nameof(TranslateGatewaySucceededHost), StringComparison.Ordinal))
                    return;

                if (logLevel == LogLevel.Warning)
                    _messages.Add(formatter(state, exception));
                else if (logLevel == LogLevel.Error)
                    _errors.Add(formatter(state, exception));
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
