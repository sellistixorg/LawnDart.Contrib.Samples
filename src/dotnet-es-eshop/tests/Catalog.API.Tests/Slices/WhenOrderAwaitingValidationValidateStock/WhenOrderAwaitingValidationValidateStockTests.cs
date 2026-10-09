using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.WhenOrderAwaitingValidationValidateStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.WhenOrderAwaitingValidationValidateStock;

public class WhenOrderAwaitingValidationValidateStockTests
{
    [Fact]
    public async Task Awaiting_validation_issues_validate_order_stock_with_the_lines()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        await AddItem(ctx, bootId, 4);
        await AddItem(ctx, capId, 9);
        var awaiting = Awaiting(orderId, new StockLine(bootId, 4), new StockLine(capId, 2));

        var commands = await ReactAsync(awaiting);
        var command = Assert.IsType<ValidateOrderStockCommand>(Assert.Single(commands));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(WhenOrderAwaitingValidationValidateStockReactor.CommandIdFor(orderId), command.CommandId);
        Assert.Equal(2, command.OrderStockItems.Count);
        Assert.Equal(bootId, command.OrderStockItems[0].ProductId);
        Assert.Equal(4, command.OrderStockItems[0].Units);
        Assert.Equal(capId, command.OrderStockItems[1].ProductId);
        Assert.Equal(2, command.OrderStockItems[1].Units);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
        var confirmed = Assert.IsType<OrderStockConfirmed>(Assert.Single(recorded).Event);
        Assert.Equal(orderId, confirmed.OrderId);
        var check = await ctx.AggregateRepository.GetOrCreateAsync<OrderStockValidation>(orderId);
        Assert.True(check.State.Checked);
        Assert.True(check.State.Confirmed);
    }

    [Fact]
    public async Task Redelivered_order_keeps_one_result()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 4);
        var first = Awaiting(orderId, new StockLine(bootId, 2));
        var second = Awaiting(orderId, new StockLine(bootId, 2));

        var firstCommands = await ReactAsync(first);
        var secondCommands = await ReactAsync(second);
        var firstCommand = Assert.IsType<ValidateOrderStockCommand>(Assert.Single(firstCommands));
        var secondCommand = Assert.IsType<ValidateOrderStockCommand>(Assert.Single(secondCommands));
        Assert.Equal(firstCommand.CommandId, secondCommand.CommandId);
        Assert.Equal(WhenOrderAwaitingValidationValidateStockReactor.CommandIdFor(orderId), firstCommand.CommandId);

        await DispatchAsync(ctx, firstCommands);
        await DispatchAsync(ctx, secondCommands);

        Assert.Single(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId)));
        var check = await ctx.AggregateRepository.GetOrCreateAsync<OrderStockValidation>(orderId);
        Assert.Equal(1, check.Version);
        Assert.True(check.State.Confirmed);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 4);
        var awaiting = new OrderAwaitingValidation(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.Empty,
            [new StockLine(bootId, 1)],
            Guid.NewGuid());
        var logger = new ListLogger();

        var commands = await ReactAsync(awaiting, logger);

        Assert.Empty(commands);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(Guid.Empty)));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(awaiting.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_line_list_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var awaiting = new OrderAwaitingValidation(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            [],
            Guid.NewGuid());
        var logger = new ListLogger();

        var commands = await ReactAsync(awaiting, logger);

        Assert.Empty(commands);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId)));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(awaiting.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty line list", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Command_rejection_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var awaiting = Awaiting(orderId, new StockLine(Guid.NewGuid(), 0));
        var logger = new ListLogger();

        var commands = await ReactAsync(awaiting, logger);

        Assert.Empty(commands);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId)));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(awaiting.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(OrderStockValidation.UnitsInvalidTitle, message, StringComparison.Ordinal);
        Assert.Contains("skipped invalid stock input", message, StringComparison.Ordinal);
        Assert.Contains("AwaitingValidation", message, StringComparison.Ordinal);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(OrderStockConfirmed),
            typeof(OrderStockRejected));

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderAwaitingValidation awaiting,
        ILogger<WhenOrderAwaitingValidationValidateStockReactor>? logger = null)
    {
        var reactor = new WhenOrderAwaitingValidationValidateStockReactor(
            logger ?? NullLogger<WhenOrderAwaitingValidationValidateStockReactor>.Instance);
        var commands = await reactor.ReactAsync(awaiting, MessageContext.New());
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((ValidateOrderStockCommand)command);
    }

    private static async Task AddItem(BddTestContext ctx, Guid itemId, int availableStock)
    {
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        await ctx.AggregateRepository.HandleCommandAsync(
            item,
            new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                availableStock,
                "High-ankle boot",
                "boot.png",
                10,
                100));
    }

    private static OrderAwaitingValidation Awaiting(Guid orderId, params StockLine[] lines) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, lines, Guid.NewGuid());

    private sealed class ListLogger : ILogger<WhenOrderAwaitingValidationValidateStockReactor>
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
