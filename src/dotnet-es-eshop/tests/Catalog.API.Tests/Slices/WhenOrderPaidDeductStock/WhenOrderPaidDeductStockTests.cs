using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.WhenOrderPaidDeductStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PaidLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.WhenOrderPaidDeductStock;

public class WhenOrderPaidDeductStockTests
{
    [Fact]
    public async Task Paid_order_issues_deduct_and_removes_the_smaller_quantity()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 2);
        var paid = Paid(orderId, buyerId, new PaidLine(bootId, 5));
        var messageId = Guid.NewGuid().ToString("D");
        var dispatcher = new HandlerDispatcher(ctx.AggregateRepository);

        var commands = await ReactAsync(paid, messageId, dispatcher);

        Assert.Empty(commands);
        var command = Assert.Single(dispatcher.Commands);
        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(WhenOrderPaidDeductStockReactor.CommandIdFor(messageId), command.Id);
        Assert.Equal('5', command.Id.ToString("D")[14]);
        var line = Assert.Single(command.OrderStockItems);
        Assert.Equal(bootId, line.ProductId);
        Assert.Equal(5, line.Units);

        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(bootId);
        Assert.Equal(0, item.State.AvailableStock);
        var deducted = Assert.Single(await DeductedEvents(ctx, bootId));
        Assert.Equal(orderId, deducted.OrderId);
        var deduction = Assert.Single(deducted.Deductions);
        Assert.Equal(bootId, deduction.ItemId);
        Assert.Equal(2, deduction.UnitsRemoved);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var orderId = Guid.NewGuid();
        var paid = Paid(orderId, Guid.NewGuid(), new PaidLine(Guid.NewGuid(), 1));
        var context = new MessageContext { MessageId = messageId };
        var dispatcher = new RecordingDispatcher();

        var first = await ReactAsync(paid, context, dispatcher);
        var second = await ReactAsync(paid, context, dispatcher);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(2, dispatcher.Commands.Count);
        Assert.Equal(dispatcher.Commands[0].Id, dispatcher.Commands[1].Id);
        Assert.NotEqual(Guid.Empty, dispatcher.Commands[0].Id);
        Assert.Equal(orderId, dispatcher.Commands[0].OrderId);
        Assert.Equal(orderId, dispatcher.Commands[1].OrderId);
    }

    [Fact]
    public async Task Different_message_ids_deduct_again()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 10);
        var paid = Paid(orderId, Guid.NewGuid(), new PaidLine(bootId, 3));
        var dispatcher = new HandlerDispatcher(ctx.AggregateRepository);

        await ReactAsync(paid, Guid.NewGuid().ToString("D"), dispatcher);
        await ReactAsync(paid, Guid.NewGuid().ToString("D"), dispatcher);

        Assert.Equal(2, dispatcher.Commands.Count);
        Assert.NotEqual(dispatcher.Commands[0].Id, dispatcher.Commands[1].Id);
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(bootId);
        Assert.Equal(4, item.State.AvailableStock);
        Assert.Equal(2, (await DeductedEvents(ctx, bootId)).Count);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        var paid = Paid(Guid.NewGuid(), Guid.NewGuid(), new PaidLine(Guid.NewGuid(), 1));
        var dispatcher = new RecordingDispatcher();
        var reactor = new WhenOrderPaidDeductStockReactor(
            dispatcher,
            NullLogger<WhenOrderPaidDeductStockReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(paid, new MessageContext { MessageId = " " }));

        Assert.Contains(paid.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
        Assert.Empty(dispatcher.Commands);
    }

    [Fact]
    public async Task Zero_stock_is_logged_and_not_retried()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 0);
        var paid = Paid(orderId, Guid.NewGuid(), new PaidLine(bootId, 1));
        var logger = new ListLogger();
        var dispatcher = new HandlerDispatcher(ctx.AggregateRepository);

        var commands = await ReactAsync(paid, Guid.NewGuid().ToString("D"), dispatcher, logger);

        Assert.Empty(commands);
        Assert.Empty(await DeductedEvents(ctx, bootId));
        Assert.Equal(0, (await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(bootId)).State.AvailableStock);
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(paid.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(CatalogItem.OutOfStockMessage, message, StringComparison.Ordinal);
        Assert.Contains(CatalogItem.OutOfStockTitle, message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zero_stock_is_checked_before_a_non_positive_quantity()
    {
        await using var ctx = Context();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 0);
        var paid = Paid(Guid.NewGuid(), Guid.NewGuid(), new PaidLine(bootId, 0));
        var logger = new ListLogger();

        var commands = await ReactAsync(paid, Guid.NewGuid().ToString("D"), new HandlerDispatcher(ctx.AggregateRepository), logger);

        Assert.Empty(commands);
        Assert.Empty(await DeductedEvents(ctx, bootId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(CatalogItem.OutOfStockTitle, message, StringComparison.Ordinal);
        Assert.DoesNotContain("greater than zero", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Non_positive_quantity_is_logged_and_not_retried(int units)
    {
        await using var ctx = Context();
        var bootId = Guid.NewGuid();
        await AddItem(ctx, bootId, 5);
        var paid = Paid(Guid.NewGuid(), Guid.NewGuid(), new PaidLine(bootId, units));
        var logger = new ListLogger();

        var commands = await ReactAsync(paid, Guid.NewGuid().ToString("D"), new HandlerDispatcher(ctx.AggregateRepository), logger);

        Assert.Empty(commands);
        Assert.Equal(5, (await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(bootId)).State.AvailableStock);
        Assert.Empty(await DeductedEvents(ctx, bootId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains("Item units desired should be greater than zero.", message, StringComparison.Ordinal);
        Assert.Contains("Skipping", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_out_of_stock_line_rejects_the_command()
    {
        await using var ctx = Context();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        await AddItem(ctx, bootId, 5);
        await AddItem(ctx, capId, 0);
        var paid = Paid(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PaidLine(bootId, 1),
            new PaidLine(capId, 1));
        var logger = new ListLogger();

        var commands = await ReactAsync(paid, Guid.NewGuid().ToString("D"), new HandlerDispatcher(ctx.AggregateRepository), logger);

        Assert.Empty(commands);
        Assert.Equal(5, (await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(bootId)).State.AvailableStock);
        Assert.Empty(await DeductedEvents(ctx, bootId));
        Assert.Empty(await DeductedEvents(ctx, capId));
        Assert.Contains(CatalogItem.OutOfStockTitle, Assert.Single(logger.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_and_removed_lines_are_skipped_by_deduct()
    {
        await using var ctx = Context(typeof(CatalogItemRemoved));
        var orderId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var knownId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        await AddItem(ctx, removedId, 8);
        await AddItem(ctx, knownId, 10);
        var removed = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(removedId);
        await ctx.AggregateRepository.HandleCommandAsync(
            removed,
            new RemoveCatalogItemCommand(Guid.NewGuid(), removedId));
        var dispatcher = new HandlerDispatcher(ctx.AggregateRepository);
        var paid = Paid(
            orderId,
            Guid.NewGuid(),
            new PaidLine(removedId, 1),
            new PaidLine(unknownId, 4),
            new PaidLine(knownId, 2));

        var commands = await ReactAsync(paid, Guid.NewGuid().ToString("D"), dispatcher);

        Assert.Empty(commands);
        var command = Assert.Single(dispatcher.Commands);
        Assert.Equal(3, command.OrderStockItems.Count);
        Assert.Equal(removedId, command.OrderStockItems[0].ProductId);
        Assert.Equal(unknownId, command.OrderStockItems[1].ProductId);
        Assert.Equal(knownId, command.OrderStockItems[2].ProductId);

        var removedItem = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(removedId);
        Assert.True(removedItem.State.Removed);
        Assert.Equal(8, removedItem.State.AvailableStock);
        Assert.Empty(await DeductedEvents(ctx, removedId));
        Assert.Empty(await ctx.EventStore.ReadStreamAsync($"CatalogItem:{unknownId}"));

        var known = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(knownId);
        Assert.Equal(8, known.State.AvailableStock);
        var deduction = Assert.Single(Assert.Single(await DeductedEvents(ctx, knownId)).Deductions);
        Assert.Equal(2, deduction.UnitsRemoved);
    }

    [Fact]
    public async Task Store_failure_is_rethrown()
    {
        var paid = Paid(Guid.NewGuid(), Guid.NewGuid(), new PaidLine(Guid.NewGuid(), 1));
        var dispatcher = new ThrowingDispatcher();
        var reactor = new WhenOrderPaidDeductStockReactor(
            dispatcher,
            NullLogger<WhenOrderPaidDeductStockReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(paid, new MessageContext { MessageId = Guid.NewGuid().ToString("D") }));

        Assert.Equal("store down", error.Message);
    }

    private static BddTestContext Context(params Type[] extra) =>
        BddTestContext.CreateInMemory(
            [typeof(CatalogItemAdded), typeof(CatalogStockDeducted), .. extra]);

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderPaid paid,
        string messageId,
        ICommandDispatcher dispatcher,
        ILogger<WhenOrderPaidDeductStockReactor>? logger = null) =>
        await ReactAsync(paid, new MessageContext { MessageId = messageId }, dispatcher, logger);

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderPaid paid,
        MessageContext context,
        ICommandDispatcher dispatcher,
        ILogger<WhenOrderPaidDeductStockReactor>? logger = null)
    {
        var reactor = new WhenOrderPaidDeductStockReactor(
            dispatcher,
            logger ?? NullLogger<WhenOrderPaidDeductStockReactor>.Instance);
        var commands = await reactor.ReactAsync(paid, context);
        return commands.ToList();
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

    private static async Task<IReadOnlyList<CatalogStockDeducted>> DeductedEvents(BddTestContext ctx, Guid itemId)
    {
        var recorded = await ctx.EventStore.ReadStreamAsync($"CatalogItem:{itemId}");
        return recorded.Select(row => row.Event).OfType<CatalogStockDeducted>().ToList();
    }

    private static OrderPaid Paid(Guid orderId, Guid buyerId, params PaidLine[] lines) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, lines, buyerId);

    private sealed class HandlerDispatcher : ICommandDispatcher
    {
        private readonly DeductCatalogStockHandler _handler;

        public HandlerDispatcher(IAggregateRepository items) =>
            _handler = new DeductCatalogStockHandler(items);

        public List<DeductCatalogStockCommand> Commands { get; } = [];

        public async Task DispatchAsync(
            ICommand command,
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            var deduct = (DeductCatalogStockCommand)command;
            Commands.Add(deduct);
            await _handler.HandleAsync(deduct, cancellationToken);
        }
    }

    private sealed class RecordingDispatcher : ICommandDispatcher
    {
        public List<DeductCatalogStockCommand> Commands { get; } = [];

        public Task DispatchAsync(
            ICommand command,
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            Commands.Add((DeductCatalogStockCommand)command);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDispatcher : ICommandDispatcher
    {
        public Task DispatchAsync(
            ICommand command,
            MessageContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("store down");
    }

    private sealed class ListLogger : ILogger<WhenOrderPaidDeductStockReactor>
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
