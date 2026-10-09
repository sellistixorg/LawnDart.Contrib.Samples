using LawnDart;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderStockConfirmed;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenCatalogStockConfirmed;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenCatalogStockConfirmed;

public class WhenCatalogStockConfirmedTests
{
    [Fact]
    public async Task Stock_confirmed_issues_mark_order_stock_confirmed()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);
        await MarkAwaitingAsync(ctx, orderId);

        var messageId = Guid.NewGuid().ToString("D");
        var confirmed = Confirmed(orderId);
        var commands = await ReactAsync(ctx, confirmed, messageId);
        var command = Assert.IsType<MarkOrderStockConfirmedCommand>(Assert.Single(commands));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(WhenCatalogStockConfirmedReactor.CommandIdFor(messageId), command.Id);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var status = Assert.IsType<OrderStatusStockConfirmed>(
            Assert.Single(recorded, row => row.Event is OrderStatusStockConfirmed).Event);
        Assert.Equal(orderId, status.OrderId);
        Assert.Equal(buyerId, status.BuyerId);
        Assert.Equal(OrderAggregate.StockConfirmedStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var orderId = Guid.NewGuid();
        var confirmed = Confirmed(orderId);
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<MarkOrderStockConfirmedCommand>(
            Assert.Single(await ReactAsync(confirmed, context)));
        var second = Assert.IsType<MarkOrderStockConfirmedCommand>(
            Assert.Single(await ReactAsync(confirmed, context)));

        Assert.Equal(first.Id, second.Id);
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
        var confirmed = new OrderStockConfirmed(Guid.NewGuid(), DateTime.UtcNow, Guid.Empty);
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            confirmed,
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        Assert.Equal(OrderAggregate.AwaitingValidationStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
        Assert.Empty(await StatusEventsAsync(ctx, orderId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(confirmed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_not_awaiting_validation_is_unchanged()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);

        var commands = await ReactAsync(ctx, Confirmed(orderId), Guid.NewGuid().ToString("D"));
        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.DoesNotContain(recorded, row => row.Event is OrderStatusStockConfirmed);
        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.SubmittedStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Units);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        var confirmed = Confirmed(Guid.NewGuid());
        var reactor = new WhenCatalogStockConfirmedReactor(
            NullLogger<WhenCatalogStockConfirmedReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(confirmed, new MessageContext { MessageId = " " }));

        Assert.Contains(confirmed.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed));

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

    private static Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        OrderStockConfirmed confirmed,
        string messageId,
        ILogger<WhenCatalogStockConfirmedReactor>? logger = null)
    {
        _ = ctx;
        return ReactAsync(confirmed, new MessageContext { MessageId = messageId }, logger);
    }

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderStockConfirmed confirmed,
        MessageContext context,
        ILogger<WhenCatalogStockConfirmedReactor>? logger = null)
    {
        var reactor = new WhenCatalogStockConfirmedReactor(
            logger ?? NullLogger<WhenCatalogStockConfirmedReactor>.Instance);
        var commands = await reactor.ReactAsync(confirmed, context);
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new MarkOrderStockConfirmedHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((MarkOrderStockConfirmedCommand)command);
    }

    private static async Task<OrderAggregate> LoadAsync(BddTestContext ctx, Guid orderId) =>
        await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);

    private static async Task<IReadOnlyList<SequencedEvent>> StatusEventsAsync(BddTestContext ctx, Guid orderId)
    {
        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        return recorded.Where(row => row.Event is OrderStatusStockConfirmed).ToList();
    }

    private static OrderStockConfirmed Confirmed(Guid orderId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId);

    private sealed class ListLogger : ILogger<WhenCatalogStockConfirmedReactor>
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
