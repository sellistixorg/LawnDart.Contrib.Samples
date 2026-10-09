using LawnDart;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.ChargeOrderPayment;

public class ChargeOrderPaymentTests
{
    [Fact]
    public async Task Charge_emits_payment_charge_requested()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .When(Charge(attemptId, orderId, 42.50m, "  usd  "))
            .ThenEmittedTypes(typeof(PaymentChargeRequested))
            .ThenEmittedEvent<PaymentChargeRequested>(e =>
                e.AttemptId == attemptId
                && e.OrderId == orderId
                && e.Amount == 42.50m
                && e.Status == PaymentAttempt.RequestedStatus
                && e.Currency == "usd")
            .ThenAppendedOnlyTo($"PaymentAttempt:{attemptId}")
            .AndAssert(result =>
            {
                var requested = Assert.IsType<PaymentChargeRequested>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, requested.Id);
                Assert.True(requested.Timestamp >= before);
                Assert.True(requested.Timestamp <= DateTime.UtcNow.AddSeconds(1));

                var state = result.Aggregate.State;
                Assert.Equal(attemptId, state.AttemptId);
                Assert.Equal(orderId, state.OrderId);
                Assert.Equal(42.50m, state.Amount);
                Assert.Equal("usd", state.Currency);
                Assert.Equal(PaymentAttempt.RequestedStatus, state.Status);
                Assert.Null(state.GatewayReference);
                Assert.Null(state.FailureReason);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Blank_currency_is_stored_as_null()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .When(Charge(attemptId, Guid.NewGuid(), 10m, "   "))
            .ThenEmittedEvent<PaymentChargeRequested>(e => e.Currency is null)
            .AndAssert(result => Assert.Null(result.Aggregate.State.Currency))
            .RunAsync();
    }

    [Fact]
    public async Task Second_charge_writes_no_event()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var first = Requested(attemptId, orderId, 18m, "USD");

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(first)
            .When(Charge(attemptId, orderId, 18m, "USD"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(attemptId, state.AttemptId);
                Assert.Equal(orderId, state.OrderId);
                Assert.Equal(18m, state.Amount);
                Assert.Equal("USD", state.Currency);
                Assert.Equal(PaymentAttempt.RequestedStatus, state.Status);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Replay_restores_the_requested_attempt()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var requested = Requested(attemptId, orderId, 9m, "EUR");

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(requested)
            .When(Charge(attemptId, Guid.NewGuid(), 1m, "USD"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(requested.AttemptId, state.AttemptId);
                Assert.Equal(requested.OrderId, state.OrderId);
                Assert.Equal(requested.Amount, state.Amount);
                Assert.Equal(requested.Currency, state.Currency);
                Assert.Equal(requested.Status, state.Status);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Charge_rejects_an_empty_command_id()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .When(Charge(attemptId, Guid.NewGuid(), 5m, "USD") with { CommandId = Guid.Empty })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.CommandIdRequiredMessage, error.Message);
                Assert.Equal(PaymentAttempt.CommandIdRequiredTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Charge_rejects_an_empty_attempt_id()
    {
        await using var ctx = Context();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, Guid.NewGuid())
            .When(Charge(Guid.Empty, Guid.NewGuid(), 5m, "USD"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.AttemptIdRequiredMessage, error.Message);
                Assert.Equal(PaymentAttempt.AttemptIdRequiredTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Charge_rejects_an_empty_order_id()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .When(Charge(attemptId, Guid.Empty, 5m, "USD"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.OrderIdRequiredMessage, error.Message);
                Assert.Equal(PaymentAttempt.OrderIdRequiredTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Charge_rejects_an_amount_that_is_not_greater_than_zero(int amount)
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .When(Charge(attemptId, Guid.NewGuid(), amount, "USD"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.AmountInvalidMessage, error.Message);
                Assert.Equal(PaymentAttempt.AmountInvalidTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal("", result.Aggregate.State.Status);
            })
            .RunAsync();
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested));

    private static ChargeOrderPaymentCommand Charge(
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency) =>
        new(Guid.NewGuid(), attemptId, orderId, amount, currency, Guid.NewGuid());

    private static PaymentChargeRequested Requested(
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            attemptId,
            orderId,
            amount,
            PaymentAttempt.RequestedStatus,
            currency);
}
