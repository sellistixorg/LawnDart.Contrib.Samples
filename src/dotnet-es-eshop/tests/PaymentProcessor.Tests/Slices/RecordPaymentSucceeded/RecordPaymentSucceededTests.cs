using LawnDart;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.RecordPaymentSucceeded;

public class RecordPaymentSucceededTests
{
    [Fact]
    public async Task Record_emits_payment_succeeded()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 42.50m, "usd"))
            .When(Record(attemptId, orderId, "  gw-1  ", 42.50m))
            .ThenEmittedTypes(typeof(PaymentSucceeded))
            .ThenEmittedEvent<PaymentSucceeded>(e =>
                e.AttemptId == attemptId
                && e.OrderId == orderId
                && e.GatewayReference == "gw-1"
                && e.Amount == 42.50m)
            .ThenAppendedOnlyTo($"PaymentAttempt:{attemptId}")
            .AndAssert(result =>
            {
                var succeeded = Assert.IsType<PaymentSucceeded>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, succeeded.Id);
                Assert.True(succeeded.Timestamp >= before);
                Assert.True(succeeded.Timestamp <= DateTime.UtcNow.AddSeconds(1));

                var state = result.Aggregate.State;
                Assert.Equal(attemptId, state.AttemptId);
                Assert.Equal(orderId, state.OrderId);
                Assert.Equal(42.50m, state.Amount);
                Assert.Equal("usd", state.Currency);
                Assert.Equal(PaymentAttempt.SucceededStatus, state.Status);
                Assert.Equal("gw-1", state.GatewayReference);
                Assert.Null(state.FailureReason);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Missing_amount_keeps_the_requested_amount()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 18m, "USD"))
            .When(Record(attemptId, orderId, null, null))
            .ThenEmittedEvent<PaymentSucceeded>(e => e.Amount is null && e.GatewayReference is null)
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(18m, state.Amount);
                Assert.Equal("USD", state.Currency);
                Assert.Equal(PaymentAttempt.SucceededStatus, state.Status);
                Assert.Null(state.GatewayReference);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Blank_gateway_reference_is_stored_as_null()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 10m, "EUR"))
            .When(Record(attemptId, orderId, "   ", 11m))
            .ThenEmittedEvent<PaymentSucceeded>(e => e.GatewayReference is null && e.Amount == 11m)
            .AndAssert(result =>
            {
                Assert.Null(result.Aggregate.State.GatewayReference);
                Assert.Equal(11m, result.Aggregate.State.Amount);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Second_success_writes_no_event()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var requested = Requested(attemptId, orderId, 9m, "USD");
        var succeeded = Succeeded(attemptId, orderId, "gw-1", 9m);

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(requested, succeeded)
            .When(Record(attemptId, orderId, "gw-2", 3m))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(PaymentAttempt.SucceededStatus, state.Status);
                Assert.Equal("gw-1", state.GatewayReference);
                Assert.Equal(9m, state.Amount);
                Assert.Equal(orderId, state.OrderId);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Recorded_failure_writes_no_event()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 9m, "USD", PaymentAttempt.FailedStatus))
            .When(Record(attemptId, orderId, "gw-1", 9m))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(PaymentAttempt.FailedStatus, state.Status);
                Assert.Null(state.GatewayReference);
                Assert.Equal(9m, state.Amount);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Unknown_attempt_is_rejected()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .When(Record(attemptId, Guid.NewGuid(), "gw-1", 5m))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.AttemptNotFoundMessage, error.Message);
                Assert.Equal(PaymentAttempt.AttemptNotFoundTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal("", result.Aggregate.State.Status);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Record_rejects_an_empty_command_id()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, Guid.NewGuid(), 5m, "USD"))
            .When(Record(attemptId, Guid.NewGuid(), null, null) with { CommandId = Guid.Empty })
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
    public async Task Record_rejects_an_empty_attempt_id()
    {
        await using var ctx = Context();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, Guid.NewGuid())
            .When(Record(Guid.Empty, Guid.NewGuid(), null, null))
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
    public async Task Record_rejects_an_empty_order_id()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, Guid.NewGuid(), 5m, "USD"))
            .When(Record(attemptId, Guid.Empty, null, null))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.OrderIdRequiredMessage, error.Message);
                Assert.Equal(PaymentAttempt.OrderIdRequiredTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(PaymentAttempt.RequestedStatus, result.Aggregate.State.Status);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Record_rejects_an_order_id_that_does_not_match_the_attempt()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 5m, "USD"))
            .When(Record(attemptId, Guid.NewGuid(), "gw-1", 5m))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(PaymentAttempt.OrderMismatchMessage, error.Message);
                Assert.Equal(PaymentAttempt.OrderMismatchTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(PaymentAttempt.RequestedStatus, result.Aggregate.State.Status);
                Assert.Equal(orderId, result.Aggregate.State.OrderId);
            })
            .RunAsync();
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested), typeof(PaymentSucceeded));

    private static RecordPaymentSucceededCommand Record(
        Guid attemptId,
        Guid orderId,
        string? gatewayReference,
        decimal? amount) =>
        new(Guid.NewGuid(), attemptId, orderId, gatewayReference, amount);

    private static PaymentChargeRequested Requested(
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency,
        string status = PaymentAttempt.RequestedStatus) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            attemptId,
            orderId,
            amount,
            status,
            currency);

    private static PaymentSucceeded Succeeded(
        Guid attemptId,
        Guid orderId,
        string? gatewayReference,
        decimal? amount) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            attemptId,
            orderId,
            gatewayReference,
            amount);
}
