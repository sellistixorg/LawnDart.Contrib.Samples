using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.ChargeOrderPayment;

public class ChargeOrderPaymentHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handler_calls_the_gateway_once_and_keeps_the_flag_result(bool paymentSucceeded)
    {
        await using var ctx = Context();
        var gateway = new RecordingGateway(paymentSucceeded);
        var handler = new ChargeOrderPaymentHandler(ctx.AggregateRepository, gateway);
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var command = new ChargeOrderPaymentCommand(
            Guid.NewGuid(),
            attemptId,
            orderId,
            15.25m,
            " usd ",
            buyerId);

        await handler.HandleAsync(command);

        var request = Assert.Single(gateway.Requests);
        Assert.Equal(attemptId, request.AttemptId);
        Assert.Equal(orderId, request.OrderId);
        Assert.Equal(15.25m, request.Amount);
        Assert.Equal("usd", request.Currency);
        Assert.Equal(buyerId, request.BuyerId);

        var published = Assert.Single(gateway.Transport.Published);
        if (paymentSucceeded)
        {
            var succeeded = Assert.IsType<PaymentGatewaySucceeded>(gateway.Result);
            Assert.Equal(attemptId, succeeded.AttemptId);
            Assert.Equal(orderId, succeeded.OrderId);
            Assert.Equal(15.25m, succeeded.Amount);
            Assert.Null(succeeded.GatewayReference);

            var callback = Assert.IsType<PaymentGatewayChargeSucceeded>(published.Message);
            Assert.Equal(attemptId, callback.AttemptId);
            Assert.Equal(orderId, callback.OrderId);
            Assert.Equal(15.25m, callback.Amount);
            Assert.Equal("usd", callback.Currency);
            Assert.Null(callback.GatewayReference);
            Assert.Equal(PaymentGatewayChargeSucceeded.MessageIdFor(attemptId), published.Context.MessageId);
            Assert.Equal(PaymentGatewayChargeSucceeded.Token, EventTypeCatalog.TryGetDeclaredName(typeof(PaymentGatewayChargeSucceeded)));
        }
        else
        {
            var failed = Assert.IsType<PaymentGatewayFailed>(gateway.Result);
            Assert.Equal(attemptId, failed.AttemptId);
            Assert.Equal(orderId, failed.OrderId);
            Assert.Equal(SimulatedPaymentGateway.DeclinedReason, failed.FailureReason);

            var callback = Assert.IsType<PaymentGatewayChargeFailed>(published.Message);
            Assert.Equal(attemptId, callback.AttemptId);
            Assert.Equal(orderId, callback.OrderId);
            Assert.Equal(15.25m, callback.Amount);
            Assert.Equal("usd", callback.Currency);
            Assert.Null(callback.GatewayReference);
            Assert.Equal(SimulatedPaymentGateway.DeclinedReason, callback.FailureReason);
            Assert.Equal(PaymentGatewayChargeFailed.MessageIdFor(attemptId), published.Context.MessageId);
            Assert.Equal(PaymentGatewayChargeFailed.Token, EventTypeCatalog.TryGetDeclaredName(typeof(PaymentGatewayChargeFailed)));
        }

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
        Assert.Equal(1, attempt.Version);
    }

    [Fact]
    public async Task Empty_buyer_id_is_omitted_from_the_gateway_request()
    {
        await using var ctx = Context();
        var gateway = new RecordingGateway(paymentSucceeded: true);
        var handler = new ChargeOrderPaymentHandler(ctx.AggregateRepository, gateway);
        var command = new ChargeOrderPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            3m,
            null,
            Guid.Empty);

        await handler.HandleAsync(command);

        var request = Assert.Single(gateway.Requests);
        Assert.Null(request.BuyerId);
        Assert.Null(request.Currency);
    }

    [Fact]
    public async Task Retry_while_requested_charges_again_with_the_same_message_id()
    {
        await using var ctx = Context();
        var gateway = new RecordingGateway(paymentSucceeded: true);
        var handler = new ChargeOrderPaymentHandler(ctx.AggregateRepository, gateway);
        var attemptId = Guid.NewGuid();
        var command = new ChargeOrderPaymentCommand(
            Guid.NewGuid(),
            attemptId,
            Guid.NewGuid(),
            8m,
            "USD",
            Guid.NewGuid());

        await handler.HandleAsync(command);
        await handler.HandleAsync(command with { CommandId = Guid.NewGuid(), Amount = 9m });

        Assert.Equal(2, gateway.Requests.Count);
        Assert.All(gateway.Requests, request => Assert.Equal(8m, request.Amount));
        Assert.Equal(2, gateway.Transport.Published.Count);
        var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);
        Assert.All(gateway.Transport.Published, item => Assert.Equal(messageId, item.Context.MessageId));
        Assert.All(
            gateway.Transport.Published,
            item => Assert.IsType<PaymentGatewayChargeSucceeded>(item.Message));

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(1, attempt.Version);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
    }

    [Fact]
    public async Task Retry_after_a_gateway_failure_charges_again()
    {
        await using var ctx = Context();
        var gateway = new RecordingGateway(paymentSucceeded: false) { FailuresRemaining = 1 };
        var handler = new ChargeOrderPaymentHandler(ctx.AggregateRepository, gateway);
        var attemptId = Guid.NewGuid();
        var command = new ChargeOrderPaymentCommand(
            Guid.NewGuid(),
            attemptId,
            Guid.NewGuid(),
            4m,
            "EUR",
            null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Empty(gateway.Transport.Published);

        await handler.HandleAsync(command);

        var published = Assert.Single(gateway.Transport.Published);
        var callback = Assert.IsType<PaymentGatewayChargeFailed>(published.Message);
        Assert.Equal(attemptId, callback.AttemptId);
        Assert.Equal(4m, callback.Amount);
        Assert.Equal("EUR", callback.Currency);
        Assert.Equal(PaymentGatewayChargeFailed.MessageIdFor(attemptId), published.Context.MessageId);

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(1, attempt.Version);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
    }

    [Fact]
    public async Task Invalid_amount_does_not_call_the_gateway()
    {
        await using var ctx = Context();
        var gateway = new RecordingGateway(paymentSucceeded: true);
        var handler = new ChargeOrderPaymentHandler(ctx.AggregateRepository, gateway);
        var command = new ChargeOrderPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            "USD",
            null);

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Equal(PaymentAttempt.AmountInvalidTitle, error.Title);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public void Assembly_scan_registers_the_charge_command_and_event()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        var assembly = typeof(PaymentChargeRequested).Assembly;

        context.AddPaymentContext(assembly, typeof(OrderStarted).Assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("payment-charge-requested", out var eventType));
        Assert.Equal(typeof(PaymentChargeRequested), eventType);
        Assert.False(context.EventCatalog.TryResolveType(PaymentGatewayChargeSucceeded.Token, out _));
        Assert.False(context.EventCatalog.TryResolveType(PaymentGatewayChargeFailed.Token, out _));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<ChargeOrderPaymentCommand>));
    }

    [Fact]
    public void Module_registers_the_simulated_gateway_with_success_by_default()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IMessageTransport, RecordingTransport>();
        builder.AddEShopSlices(typeof(PaymentChargeRequested).Assembly);

        using var provider = builder.Services.BuildServiceProvider();
        Assert.IsType<SimulatedPaymentGateway>(provider.GetRequiredService<IPaymentGateway>());
        Assert.True(provider.GetRequiredService<IOptions<PaymentOptions>>().Value.PaymentSucceeded);
    }

    [Fact]
    public void Module_binds_a_failed_payment_flag()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["PaymentOptions:PaymentSucceeded"] = "false";
        builder.AddEShopSlices(typeof(PaymentChargeRequested).Assembly);

        using var provider = builder.Services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IOptions<PaymentOptions>>().Value.PaymentSucceeded);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested));

    private sealed class RecordingGateway : IPaymentGateway
    {
        private readonly SimulatedPaymentGateway _inner;

        public RecordingGateway(bool paymentSucceeded)
        {
            Transport = new RecordingTransport();
            _inner = new SimulatedPaymentGateway(
                Options.Create(new PaymentOptions { PaymentSucceeded = paymentSucceeded }),
                Transport);
        }

        public RecordingTransport Transport { get; }

        public int FailuresRemaining
        {
            get => Transport.FailuresRemaining;
            set => Transport.FailuresRemaining = value;
        }

        public List<PaymentChargeRequest> Requests { get; } = [];

        public PaymentGatewayResult? Result { get; private set; }

        public async Task<PaymentGatewayResult> ChargeAsync(
            PaymentChargeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            Result = await _inner.ChargeAsync(request, cancellationToken);
            return Result;
        }
    }

    private sealed class RecordingTransport : IMessageTransport
    {
        public int FailuresRemaining { get; set; }

        public List<(object Message, MessageContext Context)> Published { get; } = [];

        public Task PublishAsync<TMessage>(
            TMessage message,
            MessageContext context,
            CancellationToken cancellationToken = default)
            where TMessage : class
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("publish failed");
            }

            Published.Add((message, context));
            return Task.CompletedTask;
        }

        public Task SubscribeAsync<TMessage>(
            Func<TMessage, MessageContext, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TMessage : class
            => Task.CompletedTask;
    }
}
