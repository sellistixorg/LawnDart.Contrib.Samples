using System.Text.Json;
using LawnDart;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenCatalogStockRejected;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenCatalogStockRejected;

public class WhenCatalogStockRejectedTests
{
    [Fact]
    public async Task Stock_rejected_issues_cancel_with_only_out_of_stock_product_ids()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        await PlaceAsync(
            ctx,
            orderId,
            buyerId,
            [
                new OrderLine(bootId, "Alpine boot", 120m, 2),
                new OrderLine(capId, "Wool cap", 25m, 1)
            ]);
        await MarkAwaitingAsync(ctx, orderId);

        var messageId = Guid.NewGuid().ToString("D");
        var rejected = Rejected(
            orderId,
            new ConfirmedOrderStockItem(bootId, true),
            new ConfirmedOrderStockItem(capId, false),
            new ConfirmedOrderStockItem(missingId, false));
        var commands = await ReactAsync(ctx, rejected, messageId);
        var command = Assert.IsType<CancelOrderForInsufficientStockCommand>(Assert.Single(commands));

        Assert.Equal(orderId, command.OrderId);
        Assert.Equal(WhenCatalogStockRejectedReactor.CommandIdFor(messageId), command.Id);
        Assert.Equal([capId, missingId], command.RejectedProductIds);

        var commandJson = JsonSerializer.Serialize(command);
        Assert.Contains("RejectedProductIds", commandJson, StringComparison.Ordinal);
        Assert.Contains(capId.ToString("D"), commandJson, StringComparison.Ordinal);
        Assert.Contains(missingId.ToString("D"), commandJson, StringComparison.Ordinal);
        Assert.DoesNotContain(bootId.ToString("D"), commandJson, StringComparison.Ordinal);
        Assert.DoesNotContain("HasStock", commandJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CardNumber", commandJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", commandJson, StringComparison.OrdinalIgnoreCase);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var cancelled = Assert.IsType<OrderCancelledForStock>(
            Assert.Single(recorded, row => row.Event is OrderCancelledForStock).Event);
        Assert.Equal(orderId, cancelled.OrderId);
        Assert.Equal([capId, missingId], cancelled.RejectedProductIds);
        Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);

        var cancelledJson = JsonSerializer.Serialize(cancelled);
        Assert.Contains("RejectedProductIds", cancelledJson, StringComparison.Ordinal);
        Assert.Contains(capId.ToString("D"), cancelledJson, StringComparison.Ordinal);
        Assert.Contains(missingId.ToString("D"), cancelledJson, StringComparison.Ordinal);
        Assert.DoesNotContain(bootId.ToString("D"), cancelledJson, StringComparison.Ordinal);
        Assert.DoesNotContain("HasStock", cancelledJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CardNumber", cancelledJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", cancelledJson, StringComparison.OrdinalIgnoreCase);

        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.CancelledStatus, order.State.OrderStatus);
        Assert.Equal(
            "The product items don't have stock: (Wool cap, " + missingId.ToString("D") + ").",
            order.State.Description);
        Assert.Equal(buyerId, order.State.BuyerId);
        Assert.Equal(2, order.State.OrderItems.Count);
        Assert.Equal(bootId, order.State.OrderItems[0].ProductId);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var rejected = Rejected(orderId, new ConfirmedOrderStockItem(productId, false));
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<CancelOrderForInsufficientStockCommand>(
            Assert.Single(await ReactAsync(rejected, context)));
        var second = Assert.IsType<CancelOrderForInsufficientStockCommand>(
            Assert.Single(await ReactAsync(rejected, context)));

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(orderId, first.OrderId);
        Assert.Equal([productId], first.RejectedProductIds);
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, Guid.NewGuid(), [new OrderLine(productId, "Alpine boot", 120m, 2)]);
        await MarkAwaitingAsync(ctx, orderId);
        var rejected = new OrderStockRejected(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.Empty,
            [new ConfirmedOrderStockItem(productId, false)]);
        var logger = new ListLogger();

        var commands = await ReactAsync(
            ctx,
            rejected,
            Guid.NewGuid().ToString("D"),
            logger);

        Assert.Empty(commands);
        Assert.Equal(OrderAggregate.AwaitingValidationStatus, (await LoadAsync(ctx, orderId)).State.OrderStatus);
        Assert.Empty(await CancelledEventsAsync(ctx, orderId));
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(rejected.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_not_awaiting_validation_is_unchanged()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(ctx, orderId, buyerId, [new OrderLine(productId, "Alpine boot", 120m, 2)]);

        var commands = await ReactAsync(
            ctx,
            Rejected(orderId, new ConfirmedOrderStockItem(productId, false)),
            Guid.NewGuid().ToString("D"));
        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        Assert.DoesNotContain(recorded, row => row.Event is OrderCancelledForStock);
        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.SubmittedStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Units);
    }

    [Fact]
    public async Task Every_product_in_stock_still_cancels_with_an_empty_product_list()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        await PlaceAsync(
            ctx,
            orderId,
            buyerId,
            [
                new OrderLine(bootId, "Alpine boot", 120m, 2),
                new OrderLine(capId, "Wool cap", 25m, 1)
            ]);
        await MarkAwaitingAsync(ctx, orderId);

        var commands = await ReactAsync(
            ctx,
            Rejected(
                orderId,
                new ConfirmedOrderStockItem(bootId, true),
                new ConfirmedOrderStockItem(capId, true)),
            Guid.NewGuid().ToString("D"));
        var command = Assert.IsType<CancelOrderForInsufficientStockCommand>(Assert.Single(commands));
        Assert.Empty(command.RejectedProductIds!);

        await DispatchAsync(ctx, commands);

        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        var cancelled = Assert.IsType<OrderCancelledForStock>(
            Assert.Single(recorded, row => row.Event is OrderCancelledForStock).Event);
        Assert.Empty(cancelled.RejectedProductIds);
        Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);
        var order = await LoadAsync(ctx, orderId);
        Assert.Equal(OrderAggregate.CancelledStatus, order.State.OrderStatus);
        Assert.Equal("The product items don't have stock: ().", order.State.Description);
        Assert.Equal(buyerId, order.State.BuyerId);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        var productId = Guid.NewGuid();
        var rejected = Rejected(Guid.NewGuid(), new ConfirmedOrderStockItem(productId, false));
        var reactor = new WhenCatalogStockRejectedReactor(
            NullLogger<WhenCatalogStockRejectedReactor>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(rejected, new MessageContext { MessageId = " " }));

        Assert.Contains(rejected.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderCancelledForStock));

    private static async Task PlaceAsync(
        BddTestContext ctx,
        Guid orderId,
        Guid buyerId,
        IReadOnlyList<OrderLine> lines)
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
            lines));
    }

    private static async Task MarkAwaitingAsync(BddTestContext ctx, Guid orderId)
    {
        var handler = new MarkOrderAwaitingValidationHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));
    }

    private static Task<IReadOnlyList<ICommand>> ReactAsync(
        BddTestContext ctx,
        OrderStockRejected rejected,
        string messageId,
        ILogger<WhenCatalogStockRejectedReactor>? logger = null)
    {
        _ = ctx;
        return ReactAsync(rejected, new MessageContext { MessageId = messageId }, logger);
    }

    private static async Task<IReadOnlyList<ICommand>> ReactAsync(
        OrderStockRejected rejected,
        MessageContext context,
        ILogger<WhenCatalogStockRejectedReactor>? logger = null)
    {
        var reactor = new WhenCatalogStockRejectedReactor(
            logger ?? NullLogger<WhenCatalogStockRejectedReactor>.Instance);
        var commands = await reactor.ReactAsync(rejected, context);
        return commands.ToList();
    }

    private static async Task DispatchAsync(BddTestContext ctx, IEnumerable<ICommand> commands)
    {
        var handler = new CancelOrderForInsufficientStockHandler(ctx.AggregateRepository);
        foreach (var command in commands)
            await handler.HandleAsync((CancelOrderForInsufficientStockCommand)command);
    }

    private static async Task<OrderAggregate> LoadAsync(BddTestContext ctx, Guid orderId) =>
        await ctx.AggregateRepository.GetOrCreateAsync<OrderAggregate>(orderId);

    private static async Task<IReadOnlyList<SequencedEvent>> CancelledEventsAsync(BddTestContext ctx, Guid orderId)
    {
        var recorded = await ctx.EventStore.ReadStreamAsync($"Order:{orderId}");
        return recorded.Where(row => row.Event is OrderCancelledForStock).ToList();
    }

    private static OrderStockRejected Rejected(Guid orderId, params ConfirmedOrderStockItem[] items) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, items);

    private sealed class ListLogger : ILogger<WhenCatalogStockRejectedReactor>
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
