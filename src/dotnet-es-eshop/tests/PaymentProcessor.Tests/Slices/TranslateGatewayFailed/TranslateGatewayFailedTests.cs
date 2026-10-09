using LawnDart;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.RecordPaymentFailed;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewayFailed;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.TranslateGatewayFailed;

public class TranslateGatewayFailedTests
{
    [Fact]
    public async Task Gateway_failure_issues_record_payment_failed()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var messageId = PaymentGatewayChargeFailed.MessageIdFor(attemptId);
        await Charge(ctx, attemptId, orderId, 42.50m, "usd");

        var commands = await ReactAsync(ctx, Failed(attemptId, orderId, "  card declined  ", "gw-1"), messageId);
        var command = Assert.IsType<RecordPaymentFailedCommand>(Assert.Single(commands));

        Assert.Equal(attemptId, command.AttemptId);
        Assert.Equal(orderId, command.OrderId);
        Assert.Equal("  card declined  ", command.FailureReason);
        Assert.Equal(CommandId(messageId), command.Id);
        Assert.Equal('5', command.Id.ToString("D")[14]);

        await DispatchAsync(ctx, commands);

        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(2, attempt.Version);
        Assert.Equal(PaymentAttempt.FailedStatus, attempt.State.Status);
        Assert.Equal(orderId, attempt.State.OrderId);
        Assert.Equal(42.50m, attempt.State.Amount);
        Assert.Equal("usd", attempt.State.Currency);
        Assert.Equal("card declined", attempt.State.FailureReason);
        var recorded = await ctx.EventStore.ReadStreamAsync($"PaymentAttempt:{attemptId}");
        var failed = Assert.IsType<PaymentFailed>(recorded[1].Event);
        Assert.Equal(attemptId, failed.AttemptId);
        Assert.Equal(orderId, failed.OrderId);
        Assert.Equal("card declined", failed.FailureReason);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString("D");
        await Charge(ctx, attemptId, orderId, 10m, "USD");
        var failed = Failed(attemptId, orderId, "declined", null);
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<RecordPaymentFailedCommand>(Assert.Single(await ReactAsync(ctx, failed, context)));
        var second = Assert.IsType<RecordPaymentFailedCommand>(Assert.Single(await ReactAsync(ctx, failed, context)));

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(attemptId, first.AttemptId);
        Assert.Equal(orderId, first.OrderId);
        Assert.Equal("declined", first.FailureReason);
    }

    [Fact]
    public async Task Different_message_ids_are_different_commands()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 8m, "USD");
        var failed = Failed(attemptId, orderId, "declined", null);

        var first = Assert.IsType<RecordPaymentFailedCommand>(
            Assert.Single(await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"))));
        var second = Assert.IsType<RecordPaymentFailedCommand>(
            Assert.Single(await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"))));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(attemptId, first.AttemptId);
        Assert.Equal(attemptId, second.AttemptId);
        Assert.Equal(orderId, first.OrderId);
    }

    [Fact]
    public async Task Unknown_attempt_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            Failed(attemptId, Guid.NewGuid(), "declined", null),
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(attemptId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.AttemptNotFoundMessage, message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.AttemptNotFoundTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
        Assert.Equal(0, (await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId)).Version);
    }

    [Fact]
    public async Task Mismatched_order_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 4m, "USD");
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            Failed(attemptId, Guid.NewGuid(), "declined", null),
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(attemptId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.OrderMismatchMessage, message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.OrderMismatchTitle, message, StringComparison.Ordinal);
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
        Assert.Equal(orderId, attempt.State.OrderId);
    }

    [Fact]
    public async Task Already_failed_attempt_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 6m, "USD");
        await new RecordPaymentFailedHandler(ctx.AggregateRepository)
            .HandleAsync(new RecordPaymentFailedCommand(Guid.NewGuid(), attemptId, orderId, "first"));
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            Failed(attemptId, orderId, "second", null),
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(attemptId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.FailedStatus, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(2, attempt.Version);
        Assert.Equal("first", attempt.State.FailureReason);
    }

    [Fact]
    public async Task Already_succeeded_attempt_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 6m, "USD");
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        await ctx.AggregateRepository.HandleCommandAsync(
            attempt,
            new RecordPaymentSucceededCommand(Guid.NewGuid(), attemptId, orderId, "ref", 6m));
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            Failed(attemptId, orderId, "declined", null),
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(PaymentAttempt.SucceededStatus, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
        attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(PaymentAttempt.SucceededStatus, attempt.State.Status);
        Assert.Equal(2, attempt.Version);
    }

    [Fact]
    public async Task Empty_attempt_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            Failed(Guid.Empty, Guid.NewGuid(), "declined", null),
            " ",
            logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(PaymentAttempt.AttemptIdRequiredTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            Failed(attemptId, Guid.Empty, "declined", null),
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(attemptId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.OrderIdRequiredTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        await using var ctx = Context();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await Charge(ctx, attemptId, orderId, 4m, "USD");
        var reactor = new TranslateGatewayFailedReactor(
            ctx.AggregateRepository,
            NullLogger<TranslateGatewayFailedReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(
                Failed(attemptId, orderId, "declined", null),
                new MessageContext { MessageId = " " }));

        Assert.Contains(attemptId.ToString("D"), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId);
        Assert.Equal(1, attempt.Version);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(PaymentChargeRequested),
            typeof(PaymentFailed),
            typeof(PaymentSucceeded));

    private static Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        PaymentGatewayChargeFailed failed,
        string messageId,
        ILogger<TranslateGatewayFailedReactor>? logger = null) =>
        ReactAsync(ctx, failed, new MessageContext { MessageId = messageId }, logger);

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        PaymentGatewayChargeFailed failed,
        MessageContext context,
        ILogger<TranslateGatewayFailedReactor>? logger = null)
    {
        var reactor = new TranslateGatewayFailedReactor(
            ctx.AggregateRepository,
            logger ?? NullLogger<TranslateGatewayFailedReactor>.Instance);
        var commands = await reactor.ReactAsync(failed, context);
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new RecordPaymentFailedHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((RecordPaymentFailedCommand)command);
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

    private static PaymentGatewayChargeFailed Failed(
        Guid attemptId,
        Guid orderId,
        string? reason,
        string? gatewayReference) =>
        new(attemptId, orderId, 42.50m, "usd", gatewayReference, reason);

    private static Guid CommandId(string messageId) =>
        EShopVersion5Id.ForPrefixedName(PaymentGatewayChargeFailed.Token, messageId);

    private sealed class ListLogger : ILogger<TranslateGatewayFailedReactor>
    {
        public List<string> Warnings { get; } = [];

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
                Warnings.Add(formatter(state, exception));
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
