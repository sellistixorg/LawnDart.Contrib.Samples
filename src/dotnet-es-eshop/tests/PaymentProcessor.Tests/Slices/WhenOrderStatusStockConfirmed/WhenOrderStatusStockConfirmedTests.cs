using LawnDart;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.WhenOrderStatusStockConfirmed;

public class WhenOrderStatusStockConfirmedTests
{
    [Fact]
    public async Task Stock_confirmed_issues_charge_order_payment()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString("D");
        var confirmed = Confirmed(orderId, buyerId, 42.50m);

        var commands = await ReactAsync(confirmed, messageId);
        var command = Assert.IsType<ChargeOrderPaymentCommand>(Assert.Single(commands));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(42.50m, command.Amount);
        Assert.Equal(buyerId, command.BuyerId);
        Assert.Null(command.Currency);
        Assert.Equal(CommandId(messageId), command.Id);
        Assert.Equal(AttemptId(messageId), command.AttemptId);
        Assert.NotEqual(command.Id, command.AttemptId);
        Assert.Equal('5', command.Id.ToString("D")[14]);
        Assert.Equal('5', command.AttemptId.ToString("D")[14]);

        var gateway = new RecordingGateway();
        await DispatchAsync(ctx, commands, gateway);

        var recorded = await ctx.EventStore.ReadStreamAsync($"PaymentAttempt:{command.AttemptId}");
        var requested = Assert.IsType<PaymentChargeRequested>(Assert.Single(recorded).Event);
        Assert.Equal(command.AttemptId, requested.AttemptId);
        Assert.Equal(orderId, requested.OrderId);
        Assert.Equal(42.50m, requested.Amount);
        Assert.Equal(PaymentAttempt.RequestedStatus, requested.Status);
        Assert.Null(requested.Currency);
        var attempt = await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(command.AttemptId);
        Assert.Equal(PaymentAttempt.RequestedStatus, attempt.State.Status);
        Assert.Equal(buyerId, Assert.Single(gateway.Requests).BuyerId);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command_and_attempt()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var confirmed = Confirmed(orderId, buyerId, 10m);
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<ChargeOrderPaymentCommand>(Assert.Single(await ReactAsync(confirmed, context)));
        var second = Assert.IsType<ChargeOrderPaymentCommand>(Assert.Single(await ReactAsync(confirmed, context)));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.AttemptId, second.AttemptId);
        Assert.NotEqual(first.Id, first.AttemptId);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, first.AttemptId);
        Assert.Equal(orderId, first.OrderId);
        Assert.Equal(10m, first.Amount);
        Assert.Equal(buyerId, first.BuyerId);
        Assert.Null(first.Currency);
    }

    [Fact]
    public async Task Different_message_ids_are_different_attempts()
    {
        var orderId = Guid.NewGuid();
        var confirmed = Confirmed(orderId, Guid.NewGuid(), 8m);

        var first = Assert.IsType<ChargeOrderPaymentCommand>(
            Assert.Single(await ReactAsync(confirmed, Guid.NewGuid().ToString("D"))));
        var second = Assert.IsType<ChargeOrderPaymentCommand>(
            Assert.Single(await ReactAsync(confirmed, Guid.NewGuid().ToString("D"))));

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.AttemptId, second.AttemptId);
        Assert.Equal(orderId, first.OrderId);
        Assert.Equal(orderId, second.OrderId);
    }

    [Fact]
    public async Task Empty_buyer_id_is_still_issued()
    {
        var orderId = Guid.NewGuid();
        var confirmed = Confirmed(orderId, Guid.Empty, 3m);

        var command = Assert.IsType<ChargeOrderPaymentCommand>(
            Assert.Single(await ReactAsync(confirmed, Guid.NewGuid().ToString("D"))));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(Guid.Empty, command.BuyerId);
        Assert.Equal(3m, command.Amount);
        Assert.Null(command.Currency);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        var confirmed = Confirmed(Guid.Empty, Guid.NewGuid(), 12m);
        var logger = new ListLogger();

        var commands = await ReactAsync(confirmed, " ", logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(confirmed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.OrderIdRequiredTitle, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Amount_that_is_not_greater_than_zero_is_logged_and_skipped(int amount)
    {
        var orderId = Guid.NewGuid();
        var confirmed = Confirmed(orderId, Guid.NewGuid(), amount);
        var logger = new ListLogger();

        var commands = await ReactAsync(confirmed, Guid.NewGuid().ToString("D"), logger);

        Assert.Empty(commands);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(confirmed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.AmountInvalidMessage, message, StringComparison.Ordinal);
        Assert.Contains(PaymentAttempt.AmountInvalidTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Second_delivery_of_the_same_attempt_writes_once()
    {
        await using var ctx = Context();
        var messageId = Guid.NewGuid().ToString("D");
        var confirmed = Confirmed(Guid.NewGuid(), Guid.NewGuid(), 19m);

        var first = await ReactAsync(confirmed, messageId);
        var second = await ReactAsync(confirmed, messageId);
        Assert.Equal(
            Assert.Single(first).Id,
            Assert.IsType<ChargeOrderPaymentCommand>(Assert.Single(second)).Id);

        var gateway = new RecordingGateway();
        await DispatchAsync(ctx, first, gateway);
        await DispatchAsync(ctx, second, gateway);

        var attemptId = Assert.IsType<ChargeOrderPaymentCommand>(Assert.Single(first)).AttemptId;
        Assert.Single(await ctx.EventStore.ReadStreamAsync($"PaymentAttempt:{attemptId}"));
        Assert.Equal(1, (await ctx.AggregateRepository.GetOrCreateAsync<PaymentAttempt>(attemptId)).Version);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        var confirmed = Confirmed(Guid.NewGuid(), Guid.NewGuid(), 4m);
        var reactor = new WhenOrderStatusStockConfirmedReactor(
            NullLogger<WhenOrderStatusStockConfirmedReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(confirmed, new MessageContext { MessageId = " " }));

        Assert.Contains(confirmed.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(PaymentChargeRequested));

    private static Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderStatusStockConfirmed confirmed,
        string messageId,
        ILogger<WhenOrderStatusStockConfirmedReactor>? logger = null) =>
        ReactAsync(confirmed, new MessageContext { MessageId = messageId }, logger);

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderStatusStockConfirmed confirmed,
        MessageContext context,
        ILogger<WhenOrderStatusStockConfirmedReactor>? logger = null)
    {
        var reactor = new WhenOrderStatusStockConfirmedReactor(
            logger ?? NullLogger<WhenOrderStatusStockConfirmedReactor>.Instance);
        var commands = await reactor.ReactAsync(confirmed, context);
        return commands.ToList();
    }

    private static async Task DispatchAsync(
        BddTestContext ctx,
        IEnumerable<ICommand> commands,
        IPaymentGateway gateway)
    {
        var handler = new ChargeOrderPaymentHandler(ctx.AggregateRepository, gateway);
        foreach (var command in commands)
            await handler.HandleAsync((ChargeOrderPaymentCommand)command);
    }

    private static OrderStatusStockConfirmed Confirmed(Guid orderId, Guid buyerId, decimal orderTotal) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, buyerId, orderTotal, "Ada Lovelace");

    private static Guid CommandId(string messageId) =>
        EShopVersion5Id.ForPrefixedName("order-status-stock-confirmed", messageId);

    private static Guid AttemptId(string messageId) =>
        EShopVersion5Id.ForPrefixedName("payment-attempt", messageId);

    private sealed class RecordingGateway : IPaymentGateway
    {
        private readonly SimulatedPaymentGateway _inner = new(
            Options.Create(new PaymentOptions { PaymentSucceeded = true }),
            new RecordingTransport());

        public List<PaymentChargeRequest> Requests { get; } = [];

        public async Task<PaymentGatewayResult> ChargeAsync(
            PaymentChargeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return await _inner.ChargeAsync(request, cancellationToken);
        }
    }

    private sealed class RecordingTransport : IMessageTransport
    {
        public Task PublishAsync<TMessage>(
            TMessage message,
            MessageContext context,
            CancellationToken cancellationToken = default)
            where TMessage : class
            => Task.CompletedTask;

        public Task SubscribeAsync<TMessage>(
            Func<TMessage, MessageContext, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TMessage : class
            => Task.CompletedTask;
    }

    private sealed class ListLogger : ILogger<WhenOrderStatusStockConfirmedReactor>
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
