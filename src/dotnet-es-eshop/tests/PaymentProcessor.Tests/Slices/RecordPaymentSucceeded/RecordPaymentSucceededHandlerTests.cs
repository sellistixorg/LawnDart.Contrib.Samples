using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.RecordPaymentSucceeded;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.RecordPaymentSucceeded;

public class RecordPaymentSucceededHandlerTests
{
    [Fact]
    public async Task Handler_appends_payment_succeeded_on_the_attempt_stream()
    {
        await using var ctx = Context();
        var handler = new RecordPaymentSucceededHandler(ctx.AggregateRepository);
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 15.25m);

        await handler.HandleAsync(new RecordPaymentSucceededCommand(
            Guid.NewGuid(),
            attemptId,
            orderId,
            " gw-9 ",
            15.25m));

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(PaymentAttempt.SucceededStatus, attempt.State.Status);
        Assert.Equal("gw-9", attempt.State.GatewayReference);
        Assert.Equal(15.25m, attempt.State.Amount);
        Assert.Equal(2, attempt.Version);

        var events = await ctx.EventStore.ReadStreamAsync($"PaymentAttempt:{attemptId}");
        var succeeded = Assert.IsType<PaymentSucceeded>(events[1].Event);
        Assert.Equal(orderId, succeeded.OrderId);
        Assert.Equal("gw-9", succeeded.GatewayReference);
    }

    [Fact]
    public async Task Handler_rejects_an_unknown_attempt()
    {
        await using var ctx = Context();
        var handler = new RecordPaymentSucceededHandler(ctx.AggregateRepository);

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new RecordPaymentSucceededCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null)));

        Assert.Equal(PaymentAttempt.AttemptNotFoundTitle, error.Title);
    }

    [Fact]
    public async Task Second_record_leaves_the_succeeded_attempt()
    {
        await using var ctx = Context();
        var handler = new RecordPaymentSucceededHandler(ctx.AggregateRepository);
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 8m);
        var command = new RecordPaymentSucceededCommand(
            Guid.NewGuid(),
            attemptId,
            orderId,
            "gw-1",
            8m);

        await handler.HandleAsync(command);
        await handler.HandleAsync(command with { CommandId = Guid.NewGuid(), GatewayReference = "gw-2", Amount = 1m });

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(2, attempt.Version);
        Assert.Equal(PaymentAttempt.SucceededStatus, attempt.State.Status);
        Assert.Equal("gw-1", attempt.State.GatewayReference);
        Assert.Equal(8m, attempt.State.Amount);
    }

    [Fact]
    public void Assembly_scan_registers_the_record_command_and_event()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        var assembly = typeof(RecordPaymentSucceededHandler).Assembly;

        context.AddPaymentContext(assembly, typeof(PaymentSucceeded).Assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("payment-succeeded", out var eventType));
        Assert.Equal(typeof(PaymentSucceeded), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<RecordPaymentSucceededCommand>));
    }

    private static async Task Charge(BddTestContext ctx, Guid attemptId, Guid orderId, decimal amount)
    {
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        await ctx.AggregateRepository.HandleCommandAsync(
            attempt,
            new ChargeOrderPaymentCommand(
                Guid.NewGuid(),
                attemptId,
                orderId,
                amount,
                "USD",
                Guid.NewGuid()));
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested), typeof(PaymentSucceeded));
}
