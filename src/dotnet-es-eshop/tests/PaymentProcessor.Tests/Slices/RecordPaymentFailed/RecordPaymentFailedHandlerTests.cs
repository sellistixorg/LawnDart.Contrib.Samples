using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.RecordPaymentFailed;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.RecordPaymentFailed;

public class RecordPaymentFailedHandlerTests
{
    [Fact]
    public async Task Handler_appends_payment_failed_on_the_attempt_stream()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 15.25m, "usd");
        var handler = new RecordPaymentFailedHandler(ctx.AggregateRepository);

        await handler.HandleAsync(Fail(attemptId, orderId, " card declined "));

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(2, attempt.Version);
        Assert.Equal(PaymentAttempt.FailedStatus, attempt.State.Status);
        Assert.Equal(orderId, attempt.State.OrderId);
        Assert.Equal(15.25m, attempt.State.Amount);
        Assert.Equal("usd", attempt.State.Currency);
        Assert.Equal("card declined", attempt.State.FailureReason);
    }

    [Fact]
    public async Task Second_failure_leaves_the_first_result()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 8m, "USD");
        var handler = new RecordPaymentFailedHandler(ctx.AggregateRepository);

        await handler.HandleAsync(Fail(attemptId, orderId, "declined"));
        await handler.HandleAsync(Fail(attemptId, orderId, "another reason"));

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(2, attempt.Version);
        Assert.Equal(PaymentAttempt.FailedStatus, attempt.State.Status);
        Assert.Equal("declined", attempt.State.FailureReason);
    }

    [Fact]
    public async Task Unknown_attempt_throws_and_writes_nothing()
    {
        await using var ctx = Context();
        var handler = new RecordPaymentFailedHandler(ctx.AggregateRepository);
        var attemptId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(Fail(attemptId, Guid.NewGuid(), "declined")));

        Assert.Equal(PaymentAttempt.AttemptNotFoundTitle, error.Title);
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(0, attempt.Version);
    }

    [Fact]
    public async Task Mismatched_order_throws_and_leaves_the_attempt_requested()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 4m, "USD");
        var handler = new RecordPaymentFailedHandler(ctx.AggregateRepository);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(Fail(attemptId, Guid.NewGuid(), "declined")));

        Assert.Equal(PaymentAttempt.OrderMismatchTitle, error.Title);
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(1, attempt.Version);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
        Assert.Equal(orderId, attempt.State.OrderId);
    }

    [Fact]
    public void Assembly_scan_registers_the_failed_command_and_shared_event()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        var assembly = typeof(PaymentChargeRequested).Assembly;
        var shared = typeof(PaymentFailed).Assembly;
        var declared = shared.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "payment-failed")
            .ToArray();

        Assert.Single(declared);
        Assert.Equal(typeof(PaymentFailed), declared[0]);

        context.AddPaymentContext(assembly, shared);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("payment-failed", out var eventType));
        Assert.Equal(typeof(PaymentFailed), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<RecordPaymentFailedCommand>));
    }

    private static async Task Charge(
        BddTestContext ctx,
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency)
    {
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        await ctx.AggregateRepository.HandleCommandAsync(
            attempt,
            new ChargeOrderPaymentCommand(
                Guid.NewGuid(),
                attemptId,
                orderId,
                amount,
                currency,
                Guid.NewGuid()));
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested), typeof(PaymentFailed));

    private static RecordPaymentFailedCommand Fail(Guid attemptId, Guid orderId, string? reason) =>
        new(Guid.NewGuid(), attemptId, orderId, reason);
}
