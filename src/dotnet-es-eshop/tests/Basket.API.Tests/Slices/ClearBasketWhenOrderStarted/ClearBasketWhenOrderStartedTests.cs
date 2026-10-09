using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasketWhenOrderStarted;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.ClearBasketWhenOrderStarted;

public class ClearBasketWhenOrderStartedTests
{
    [Fact]
    public void Order_started_buyer_id_is_the_basket_stream_id()
    {
        var buyerId = Guid.NewGuid();
        var started = Started(buyerId);

        Assert.Equal(buyerId, started.BuyerId);
        Assert.Equal($"Basket:{buyerId:D}", BasketContentsProjection.InstanceId(started.BuyerId));
    }

    [Fact]
    public async Task Order_started_clears_the_basket_for_that_buyer()
    {
        await using var ctx = CreateContext();
        var buyerId = Guid.NewGuid();
        var otherBuyerId = Guid.NewGuid();
        await AddItemAsync(ctx, buyerId, "Alpine boot", 120m, 2);
        await AddItemAsync(ctx, buyerId, "Wool cap", 25m, 4);
        await AddItemAsync(ctx, otherBuyerId, "Trail shoe", 80m, 1);

        var started = Started(buyerId);
        var commands = await ReactAsync(ctx, started);
        var command = Assert.IsType<ClearBasketCommand>(Assert.Single(commands));
        Assert.Equal(buyerId, command.BuyerId);
        Assert.Equal(ClearBasketWhenOrderStartedReactor.Reason, command.Reason);
        Assert.Equal(BasketContentsProjection.InstanceId(started.BuyerId), $"Basket:{command.BuyerId:D}");

        await DispatchAsync(ctx, commands);

        var cleared = await ClearedEventsAsync(ctx, buyerId);
        var basketCleared = Assert.IsType<BasketCleared>(Assert.Single(cleared).Event);
        Assert.Equal(buyerId, basketCleared.BuyerId);
        Assert.Equal(ClearBasketWhenOrderStartedReactor.Reason, basketCleared.Reason);
        Assert.Empty((await LoadAsync(ctx, buyerId)).State.Items);
        Assert.Single((await LoadAsync(ctx, otherBuyerId)).State.Items);
    }

    [Fact]
    public async Task Redelivered_order_started_does_not_fail()
    {
        await using var ctx = CreateContext();
        var buyerId = Guid.NewGuid();
        await AddItemAsync(ctx, buyerId, "Alpine boot", 120m, 2);
        var started = Started(buyerId);

        await DispatchAsync(ctx, await ReactAsync(ctx, started));
        await DispatchAsync(ctx, await ReactAsync(ctx, started));

        Assert.Single(await ClearedEventsAsync(ctx, buyerId));
        Assert.Empty((await LoadAsync(ctx, buyerId)).State.Items);
    }

    [Fact]
    public async Task Unknown_buyer_does_not_fail()
    {
        await using var ctx = CreateContext();
        var buyerId = Guid.NewGuid();

        var commands = await ReactAsync(ctx, Started(buyerId));

        Assert.Empty(commands);
        Assert.Empty(await ClearedEventsAsync(ctx, buyerId));
        Assert.Equal(0, (await LoadAsync(ctx, buyerId)).Version);
    }

    [Fact]
    public async Task Empty_basket_does_not_fail()
    {
        await using var ctx = CreateContext();
        var buyerId = Guid.NewGuid();
        await AddItemAsync(ctx, buyerId, "Alpine boot", 120m, 2);
        await DispatchAsync(ctx, [new ClearBasketCommand(Guid.NewGuid(), buyerId, "UserCleared")]);

        await DispatchAsync(ctx, await ReactAsync(ctx, Started(buyerId)));

        var cleared = await ClearedEventsAsync(ctx, buyerId);
        var only = Assert.IsType<BasketCleared>(Assert.Single(cleared).Event);
        Assert.Equal("UserCleared", only.Reason);
        Assert.Empty((await LoadAsync(ctx, buyerId)).State.Items);
    }

    [Fact]
    public async Task Empty_buyer_id_is_logged_and_skipped()
    {
        await using var ctx = CreateContext();
        var buyerId = Guid.NewGuid();
        await AddItemAsync(ctx, buyerId, "Alpine boot", 120m, 2);
        var started = new OrderStarted(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.Empty);
        var logger = new ListLogger();

        var commands = await ReactAsync(ctx, started, logger);

        Assert.Empty(commands);
        Assert.Empty(await ClearedEventsAsync(ctx, buyerId));
        Assert.Single((await LoadAsync(ctx, buyerId)).State.Items);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(started.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty buyer id", message, StringComparison.Ordinal);
    }

    private static BddTestContext CreateContext() =>
        BddTestContext.CreateInMemory(typeof(BasketItemAdded), typeof(BasketCleared));

    private static async Task AddItemAsync(
        BddTestContext ctx,
        Guid buyerId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        var basket = await ctx.AggregateRepository.GetOrCreateAsync<BasketAggregate>(buyerId);
        await ctx.AggregateRepository.HandleCommandAsync(
            basket,
            new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                Guid.NewGuid(),
                productName,
                unitPrice,
                quantity,
                null));
    }

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        OrderStarted started,
        ILogger<ClearBasketWhenOrderStartedReactor>? logger = null)
    {
        var reactor = new ClearBasketWhenOrderStartedReactor(
            ctx.AggregateRepository,
            logger ?? NullLogger<ClearBasketWhenOrderStartedReactor>.Instance);
        var commands = await reactor.ReactAsync(started, MessageContext.New());
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new ClearBasketHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((ClearBasketCommand)command);
    }

    private static async Task<BasketAggregate> LoadAsync(BddTestContext ctx, Guid buyerId) =>
        await ctx.AggregateRepository.GetOrCreateAsync<BasketAggregate>(buyerId);

    private static async Task<IReadOnlyList<SequencedEvent>> ClearedEventsAsync(BddTestContext ctx, Guid buyerId)
    {
        var recorded = await ctx.EventStore.ReadStreamAsync(BasketContentsProjection.InstanceId(buyerId));
        return recorded.Where(row => row.Event is BasketCleared).ToList();
    }

    private static OrderStarted Started(Guid buyerId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), buyerId);

    private sealed class ListLogger : ILogger<ClearBasketWhenOrderStartedReactor>
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
