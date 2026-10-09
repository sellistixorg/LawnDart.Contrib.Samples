using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.ConfirmGracePeriod;

public class ConfirmGracePeriodTests
{
    [Fact]
    public async Task Confirmation_moves_a_submitted_order_to_awaiting_validation()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, productId);

        var commands = await ReactAsync(ctx, Confirmed(orderId));
        var command = Assert.IsType<MarkOrderAwaitingValidationCommand>(Assert.Single(commands));
        Assert.Equal(orderId, command.OrderId);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var awaiting = Assert.IsType<OrderAwaitingValidation>(Assert.Single(recorded, row => row.Event is OrderAwaitingValidation).Event);
        Assert.Equal(orderId, awaiting.OrderId);
        Assert.Equal(buyerId, awaiting.BuyerId);
        var stock = Assert.Single(awaiting.OrderStockItems);
        Assert.Equal(productId, stock.ProductId);
        Assert.Equal(2, stock.Units);
        Assert.Equal(OrderAggregate.AwaitingValidationStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
    }

    [Fact]
    public async Task Order_that_is_no_longer_submitted_writes_no_event()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        await DispatchAsync(ctx, [new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId)]);

        await DispatchAsync(ctx, await ReactAsync(ctx, Confirmed(orderId)));

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.Single(recorded, row => row.Event is OrderAwaitingValidation);
        Assert.Equal(OrderAggregate.AwaitingValidationStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
    }

    [Fact]
    public async Task Second_confirmation_does_not_append_again()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        var confirmed = Confirmed(orderId);

        await DispatchAsync(ctx, await ReactAsync(ctx, confirmed));
        await DispatchAsync(ctx, await ReactAsync(ctx, confirmed));

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.Single(recorded, row => row.Event is OrderAwaitingValidation);
        Assert.Equal(3, recorded.Count);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), Guid.NewGuid());
        var confirmed = new GracePeriodConfirmed(Guid.NewGuid(), DateTime.UtcNow, Guid.Empty);
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, confirmed, logger);

        Assert.Empty(commands);
        Assert.Equal(OrderAggregate.SubmittedStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(confirmed.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation));

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

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        GracePeriodConfirmed confirmed,
        ILogger<ConfirmGracePeriodReactor>? logger = null)
    {
        _ = ctx;
        var reactor = new ConfirmGracePeriodReactor(logger ?? NullLogger<ConfirmGracePeriodReactor>.Instance);
        var commands = await reactor.ReactAsync(confirmed, MessageContext.New());
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new MarkOrderAwaitingValidationHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((MarkOrderAwaitingValidationCommand)command);
    }

    private static async Task<OrderAggregate> LoadAsync(BddTestContext ctx, Guid orderId) =>
        await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);

    private static GracePeriodConfirmed Confirmed(Guid orderId) =>
        new(GracePeriodConfirmed.IdFor(orderId), DateTime.UtcNow, orderId);

    private sealed class ListLogger : ILogger<ConfirmGracePeriodReactor>
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
