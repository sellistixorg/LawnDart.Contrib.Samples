using LawnDart;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.RecordPaymentFailed;

public class RecordPaymentFailedTests
{
    [Fact]
    public async Task Gateway_declined_emits_payment_failed()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 42.50m, "usd"))
            .When(Fail(attemptId, orderId, "  card declined  "))
            .ThenEmittedTypes(typeof(PaymentFailed))
            .ThenEmittedEvent<PaymentFailed>(e =>
                e.AttemptId == attemptId
                && e.OrderId == orderId
                && e.FailureReason == "card declined")
            .ThenAppendedOnlyTo($"PaymentAttempt:{attemptId}")
            .AndAssert(result =>
            {
                var failed = Assert.IsType<PaymentFailed>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, failed.Id);
                Assert.True(failed.Timestamp >= before);
                Assert.True(failed.Timestamp <= DateTime.UtcNow.AddSeconds(1));

                var state = result.Aggregate.State;
                Assert.Equal(attemptId, state.AttemptId);
                Assert.Equal(orderId, state.OrderId);
                Assert.Equal(42.50m, state.Amount);
                Assert.Equal("usd", state.Currency);
                Assert.Equal(PaymentAttempt.FailedStatus, state.Status);
                Assert.Null(state.GatewayReference);
                Assert.Equal("card declined", state.FailureReason);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Blank_failure_reason_is_stored_as_null()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 10m, "USD"))
            .When(Fail(attemptId, orderId, "   "))
            .ThenEmittedEvent<PaymentFailed>(e => e.FailureReason is null)
            .AndAssert(result =>
            {
                Assert.Equal(PaymentAttempt.FailedStatus, result.Aggregate.State.Status);
                Assert.Null(result.Aggregate.State.FailureReason);
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
            .When(Fail(attemptId, Guid.NewGuid(), "declined"))
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
    public async Task Second_failure_writes_no_event()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var first = Failed(attemptId, orderId, "card declined");

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 18m, "USD"))
            .Given(first)
            .When(Fail(attemptId, orderId, "a different reason"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(PaymentAttempt.FailedStatus, state.Status);
                Assert.Equal(first.FailureReason, state.FailureReason);
                Assert.Equal(18m, state.Amount);
                Assert.Equal("USD", state.Currency);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Succeeded_attempt_writes_no_event()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 9m, "EUR", PaymentAttempt.SucceededStatus))
            .When(Fail(attemptId, orderId, "declined"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(PaymentAttempt.SucceededStatus, state.Status);
                Assert.Null(state.FailureReason);
                Assert.Equal(9m, state.Amount);
                Assert.Equal("EUR", state.Currency);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Replay_restores_the_failed_attempt()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var failed = Failed(attemptId, orderId, "insufficient funds");

        await AggregateSpec
            .For<PaymentAttempt>(ctx, attemptId)
            .Given(Requested(attemptId, orderId, 9m, "EUR"))
            .Given(failed)
            .When(Fail(attemptId, Guid.NewGuid(), "again"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(failed.AttemptId, state.AttemptId);
                Assert.Equal(failed.OrderId, state.OrderId);
                Assert.Equal(failed.FailureReason, state.FailureReason);
                Assert.Equal(PaymentAttempt.FailedStatus, state.Status);
                Assert.Equal(9m, state.Amount);
                Assert.Equal("EUR", state.Currency);
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
            .When(Fail(attemptId, Guid.NewGuid(), "declined") with { CommandId = Guid.Empty })
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
            .When(Fail(Guid.Empty, Guid.NewGuid(), "declined"))
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
            .When(Fail(attemptId, Guid.Empty, "declined"))
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
            .When(Fail(attemptId, Guid.NewGuid(), "declined"))
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
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested), typeof(PaymentFailed));

    private static RecordPaymentFailedCommand Fail(Guid attemptId, Guid orderId, string? reason) =>
        new(Guid.NewGuid(), attemptId, orderId, reason);

    private static PaymentChargeRequested Requested(
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency,
        string? status = null) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            attemptId,
            orderId,
            amount,
            status ?? PaymentAttempt.RequestedStatus,
            currency);

    private static PaymentFailed Failed(Guid attemptId, Guid orderId, string? reason) =>
        new(Guid.NewGuid(), DateTime.UtcNow, attemptId, orderId, reason);
}
