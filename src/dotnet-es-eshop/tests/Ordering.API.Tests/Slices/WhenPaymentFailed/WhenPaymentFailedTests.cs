using System.Text.Json;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Metadata;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderPaid;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderStockConfirmed;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentFailed;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenPaymentFailed;

public class WhenPaymentFailedTests
{
    [Fact]
    public async Task Payment_failed_issues_cancel_with_the_failure_reason()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);
        await MarkAwaitingAsync(ctx, orderId);
        await MarkStockConfirmedAsync(ctx, orderId);

        var messageId = Guid.NewGuid().ToString("D");
        var failed = Failed(orderId, "Card declined");
        var commands = await ReactAsync(ctx, failed, messageId);
        var command = Assert.IsType<CancelOrderCommand>(Assert.Single(commands));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal("Card declined", command.Reason);
        Assert.Equal(WhenPaymentFailedReactor.CommandIdFor(messageId), command.Id);
        Assert.Equal('5', command.Id.ToString("D")[14]);

        var commandJson = JsonSerializer.Serialize(command);
        Assert.Contains("Card declined", commandJson, StringComparison.Ordinal);
        Assert.DoesNotContain("4444", commandJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CardNumber", commandJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", commandJson, StringComparison.OrdinalIgnoreCase);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var cancelled = Assert.IsType<OrderCancelled>(
            Assert.Single(recorded, row => row.Event is OrderCancelled).Event);
        Assert.Equal(orderId, cancelled.OrderId);
        Assert.Equal("Card declined", cancelled.Reason);
        Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);

        var cancelledJson = JsonSerializer.Serialize(cancelled);
        Assert.Contains("Card declined", cancelledJson, StringComparison.Ordinal);
        Assert.DoesNotContain("4444", cancelledJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CardNumber", cancelledJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", cancelledJson, StringComparison.OrdinalIgnoreCase);

        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.CancelledStatus, order.State.OrderStatus);
        Assert.Equal(OrderAggregate.CancelledDescription, order.State.Description);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Units);
    }

    [Fact]
    public async Task Blank_failure_reason_is_stored_as_no_reason()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        await MarkAwaitingAsync(ctx, orderId);
        await MarkStockConfirmedAsync(ctx, orderId);

        var commands = await ReactAsync(ctx, Failed(orderId, "   "), Guid.NewGuid().ToString("D"));
        var command = Assert.IsType<CancelOrderCommand>(Assert.Single(commands));
        Assert.Equal("   ", command.Reason);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var cancelled = Assert.IsType<OrderCancelled>(
            Assert.Single(recorded, row => row.Event is OrderCancelled).Event);
        Assert.Null(cancelled.Reason);
        Assert.Equal(OrderAggregate.CancelledStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        var messageId = Guid.NewGuid().ToString("D");
        var failed = Failed(orderId, "Card declined");
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<CancelOrderCommand>(Assert.Single(await ReactAsync(ctx, failed, context)));
        var second = Assert.IsType<CancelOrderCommand>(Assert.Single(await ReactAsync(ctx, failed, context)));

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(orderId, first.OrderId);
        Assert.Equal("Card declined", first.Reason);
        Assert.Equal(orderId, second.OrderId);
    }

    [Fact]
    public async Task Different_message_ids_are_different_commands()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        var failed = Failed(orderId, "Card declined");

        var first = Assert.IsType<CancelOrderCommand>(
            Assert.Single(await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"))));
        var second = Assert.IsType<CancelOrderCommand>(
            Assert.Single(await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"))));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(orderId, first.OrderId);
        Assert.Equal(orderId, second.OrderId);
        Assert.Equal("Card declined", first.Reason);
        Assert.Equal("Card declined", second.Reason);
    }

    [Fact]
    public async Task Submitted_order_is_cancelled()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, Guid.NewGuid());

        var commands = await ReactAsync(ctx, Failed(orderId, null), Guid.NewGuid().ToString("D"));
        var command = Assert.IsType<CancelOrderCommand>(Assert.Single(commands));
        Assert.Null(command.Reason);

        await DispatchAsync(ctx, commands);

        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.CancelledStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var cancelled = Assert.IsType<OrderCancelled>(
            Assert.Single(recorded, row => row.Event is OrderCancelled).Event);
        Assert.Null(cancelled.Reason);
    }

    [Fact]
    public async Task Already_cancelled_order_issues_cancel_and_writes_no_second_event()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        await DispatchAsync(ctx, await ReactAsync(ctx, Failed(orderId, "first"), Guid.NewGuid().ToString("D")));

        var commands = await ReactAsync(ctx, Failed(orderId, "second"), Guid.NewGuid().ToString("D"));
        var command = Assert.IsType<CancelOrderCommand>(Assert.Single(commands));
        Assert.Equal("second", command.Reason);
        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var cancelled = Assert.IsType<OrderCancelled>(
            Assert.Single(recorded, row => row.Event is OrderCancelled).Event);
        Assert.Equal("first", cancelled.Reason);
        Assert.Equal(OrderAggregate.CancelledStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        var failed = Failed(Guid.Empty, "Card declined");
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"), logger);

        Assert.Empty(commands);
        Assert.Equal(OrderAggregate.SubmittedStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
        Assert.Empty(await CancelledEventsAsync(ctx, orderId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(failed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.OrderIdRequiredTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_order_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var failed = Failed(orderId, "Card declined");
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"), logger);

        Assert.Empty(commands);
        Assert.Empty(await CancelledEventsAsync(ctx, orderId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(failed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.OrderNotFoundMessage, message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.OrderNotFoundTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paid_order_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);
        await MarkAwaitingAsync(ctx, orderId);
        await MarkStockConfirmedAsync(ctx, orderId);
        await MarkPaidAsync(ctx, orderId);
        var failed = Failed(orderId, "Card declined");
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, failed, Guid.NewGuid().ToString("D"), logger);

        Assert.Empty(commands);
        Assert.Empty(await CancelledEventsAsync(ctx, orderId));
        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.PaidStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(failed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.CannotCancelOrderMessage, message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.CannotCancelOrderTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        var failed = Failed(Guid.NewGuid(), "Card declined");
        var reactor = new WhenPaymentFailedReactor(
            new ThrowingOrders(),
            NullLogger<WhenPaymentFailedReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(failed, new MessageContext { MessageId = " " }));

        Assert.Contains(failed.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Store_failure_is_rethrown()
    {
        var failed = Failed(Guid.NewGuid(), "Card declined");
        var reactor = new WhenPaymentFailedReactor(
            new ThrowingOrders(),
            NullLogger<WhenPaymentFailedReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(failed, new MessageContext { MessageId = Guid.NewGuid().ToString("D") }));

        Assert.Equal("store down", error.Message);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed),
            typeof(OrderPaid),
            typeof(OrderCancelled));

    private static async Task PlaceAsync(BddTestContext ctx, Guid orderId, Guid buyerId, Guid productId)
    {
        var handler = new PlaceOrderHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new PlaceOrderCommand(
            orderId,
            orderId,
            buyerId,
            "Ada Lovelace",
            "1 Main",
            "Seattle",
            "WA",
            "USA",
            "98101",
            1,
            "Ada Lovelace",
            "4444",
            new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            [new OrderLine(productId, "Alpine boot", 120m, 2)]));
    }

    private static async Task MarkAwaitingAsync(BddTestContext ctx, Guid orderId)
    {
        var handler = new MarkOrderAwaitingValidationHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));
    }

    private static async Task MarkStockConfirmedAsync(BddTestContext ctx, Guid orderId)
    {
        var handler = new MarkOrderStockConfirmedHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new MarkOrderStockConfirmedCommand(Guid.NewGuid(), orderId));
    }

    private static async Task MarkPaidAsync(BddTestContext ctx, Guid orderId)
    {
        var handler = new MarkOrderPaidHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new MarkOrderPaidCommand(Guid.NewGuid(), orderId));
    }

    private static Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        PaymentFailed failed,
        string messageId,
        ILogger<WhenPaymentFailedReactor>? logger = null) =>
        ReactAsync(ctx, failed, new MessageContext { MessageId = messageId }, logger);

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        PaymentFailed failed,
        MessageContext context,
        ILogger<WhenPaymentFailedReactor>? logger = null)
    {
        var reactor = new WhenPaymentFailedReactor(
            ctx.AggregateRepository,
            logger ?? NullLogger<WhenPaymentFailedReactor>.Instance);
        var commands = await reactor.ReactAsync(failed, context);
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new CancelOrderHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((CancelOrderCommand)command);
    }

    private static async Task<OrderAggregate> LoadAsync(BddTestContext ctx, Guid orderId) =>
        await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);

    private static async Task<IReadOnlyList<SequencedEvent>> CancelledEventsAsync(BddTestContext ctx, Guid orderId)
    {
        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        return recorded.Where(row => row.Event is OrderCancelled).ToList();
    }

    private static PaymentFailed Failed(Guid orderId, string? reason) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), orderId, reason);

    private sealed class ThrowingOrders : IAggregateRepository
    {
        public Task<T?> GetAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : AggregateRoot, new() =>
            throw new NotSupportedException();

        public Task<T?> GetAsync<T>(string streamId, CancellationToken cancellationToken = default)
            where T : AggregateRoot, new() =>
            throw new NotSupportedException();

        public Task<T> CreateAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : AggregateRoot, new() =>
            throw new NotSupportedException();

        public Task<T> CreateAsync<T>(string streamId, CancellationToken cancellationToken = default)
            where T : AggregateRoot, new() =>
            throw new NotSupportedException();

        public Task<T> GetOrCreateAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : AggregateRoot, new() =>
            throw new InvalidOperationException("store down");

        public Task<T> GetOrCreateAsync<T>(string streamId, CancellationToken cancellationToken = default)
            where T : AggregateRoot, new() =>
            throw new NotSupportedException();

        public Task SaveAsync(
            AggregateRoot aggregate,
            CommandMetadata commandMetadata,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<T> HandleCommandAsync<T, TCommand>(
            T aggregate,
            TCommand command,
            CommandMetadata? commandMetadata = null,
            CancellationToken cancellationToken = default)
            where T : AggregateRoot
            where TCommand : ICommand =>
            throw new NotSupportedException();
    }

    private sealed class ListLogger : ILogger<WhenPaymentFailedReactor>
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
