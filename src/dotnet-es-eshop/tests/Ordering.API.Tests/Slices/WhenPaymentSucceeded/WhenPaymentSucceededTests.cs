using LawnDart;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderPaid;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderStockConfirmed;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentSucceeded;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenPaymentSucceeded;

public class WhenPaymentSucceededTests
{
    [Fact]
    public async Task Payment_succeeded_issues_mark_order_paid()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);
        await MarkAwaitingAsync(ctx, orderId);
        await MarkStockConfirmedAsync(ctx, orderId);

        var messageId = Guid.NewGuid().ToString("D");
        var succeeded = Succeeded(orderId, "gw-42", 240m);
        var commands = await ReactAsync(ctx, succeeded, messageId);
        var command = Assert.IsType<MarkOrderPaidCommand>(Assert.Single(commands));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(CommandId(messageId), command.Id);
        Assert.Equal('5', command.Id.ToString("D")[14]);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var paid = Assert.IsType<OrderPaid>(
            Assert.Single(recorded, row => row.Event is OrderPaid).Event);
        Assert.Equal(orderId, paid.OrderId);
        Assert.Equal(buyerId, paid.BuyerId);
        var line = Assert.Single(paid.OrderStockItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Units);
        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.PaidStatus, order.State.OrderStatus);
        Assert.Equal(OrderAggregate.PaidDescription, order.State.Description);
        Assert.Equal(buyerId, order.State.BuyerId);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        await using var ctx = Context();
        var messageId = Guid.NewGuid().ToString("D");
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        var succeeded = Succeeded(orderId, null, null);
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<MarkOrderPaidCommand>(
            Assert.Single(await ReactAsync(ctx, succeeded, context)));
        var second = Assert.IsType<MarkOrderPaidCommand>(
            Assert.Single(await ReactAsync(ctx, succeeded, context)));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(CommandId(messageId), first.Id);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(orderId, first.OrderId);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        await MarkAwaitingAsync(ctx, orderId);
        await MarkStockConfirmedAsync(ctx, orderId);
        var succeeded = new PaymentSucceeded(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            Guid.Empty,
            "gw-1",
            10m);
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, succeeded, " ", logger);

        Assert.Empty(commands);
        Assert.Equal(OrderAggregate.StockConfirmedStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
        Assert.Empty(await PaidEventsAsync(ctx, orderId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(succeeded.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.OrderIdRequiredTitle, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_order_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var succeeded = Succeeded(orderId, "gw-1", 10m);
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, succeeded, Guid.NewGuid().ToString("D"), logger);

        Assert.Empty(commands);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync($"Order:{orderId}"));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(succeeded.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.OrderNotFoundMessage, message, StringComparison.Ordinal);
        Assert.Contains(OrderAggregate.OrderNotFoundTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_not_stock_confirmed_is_unchanged()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);

        var commands = await ReactAsync(ctx, Succeeded(orderId, "gw-1", 240m), Guid.NewGuid().ToString("D"));
        Assert.Single(commands);
        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.DoesNotContain(recorded, row => row.Event is OrderPaid);
        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.SubmittedStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Units);
    }

    [Fact]
    public async Task Repeat_after_paid_writes_nothing()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, Guid.NewGuid());
        await MarkAwaitingAsync(ctx, orderId);
        await MarkStockConfirmedAsync(ctx, orderId);

        await DispatchAsync(ctx, await ReactAsync(ctx, Succeeded(orderId, "gw-1", 240m), Guid.NewGuid().ToString("D")));
        var again = await ReactAsync(ctx, Succeeded(orderId, "gw-2", 240m), Guid.NewGuid().ToString("D"));
        Assert.Single(again);
        await DispatchAsync(ctx, again);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.Single(recorded, row => row.Event is OrderPaid);
        Assert.Equal(OrderAggregate.PaidStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
        Assert.Equal(buyerId, (await LoadAsync(ctx, orderId)).State.BuyerId);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        await using var ctx = Context();
        var succeeded = Succeeded(Guid.NewGuid(), "gw-1", 1m);
        var reactor = new WhenPaymentSucceededReactor(
            ctx.AggregateRepository,
            NullLogger<WhenPaymentSucceededReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(succeeded, new MessageContext { MessageId = " " }));

        Assert.Contains(succeeded.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed),
            typeof(OrderPaid));

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

    private static Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        PaymentSucceeded succeeded,
        string messageId,
        ILogger<WhenPaymentSucceededReactor>? logger = null) =>
        ReactAsync(ctx, succeeded, new MessageContext { MessageId = messageId }, logger);

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        PaymentSucceeded succeeded,
        MessageContext context,
        ILogger<WhenPaymentSucceededReactor>? logger = null)
    {
        var reactor = new WhenPaymentSucceededReactor(
            ctx.AggregateRepository,
            logger ?? NullLogger<WhenPaymentSucceededReactor>.Instance);
        var commands = await reactor.ReactAsync(succeeded, context);
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new MarkOrderPaidHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((MarkOrderPaidCommand)command);
    }

    private static async Task<OrderAggregate> LoadAsync(BddTestContext ctx, Guid orderId) =>
        await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);

    private static async Task<IReadOnlyList<SequencedEvent>> PaidEventsAsync(BddTestContext ctx, Guid orderId)
    {
        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        return recorded.Where(row => row.Event is OrderPaid).ToList();
    }

    private static PaymentSucceeded Succeeded(Guid orderId, string? gatewayReference, decimal? amount) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), orderId, gatewayReference, amount);

    private static Guid CommandId(string messageId) =>
        EShopVersion5Id.ForPrefixedName("payment-succeeded", messageId);

    private sealed class ListLogger : ILogger<WhenPaymentSucceededReactor>
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
