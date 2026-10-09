using System.Text.Json;
using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.CancelOrderForInsufficientStock;

public class CancelOrderForInsufficientStockTests
{
    [Fact]
    public async Task Awaiting_validation_cancels_and_lists_the_products()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        var lines = new List<OrderLine>
        {
            new(bootId, "Alpine boot", 120m, 2),
            new(capId, "Wool cap", 25m, 1)
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .When(Cancel(orderId, [capId, bootId]))
            .ThenEmittedTypes(typeof(OrderCancelledForStock))
            .ThenEmittedEvent<OrderCancelledForStock>(e => e.OrderId == orderId)
            .ThenAppendedOnlyTo($"Order:{orderId}")
            .AndAssert(result =>
            {
                Assert.Single(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.CancelledStatus, state.OrderStatus);
                Assert.Equal(StockDescription("Wool cap", "Alpine boot"), state.Description);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(2, state.OrderItems.Count);
                Assert.Equal("Alpine boot", state.OrderItems[0].ProductName);
                Assert.Equal(120m, state.OrderItems[0].UnitPrice);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Event_carries_rejected_product_ids_and_cancelled_status()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var lines = new List<OrderLine>
        {
            new(bootId, "  Alpine boot  ", 120m, 2)
        };
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .When(Cancel(orderId, [missingId, bootId]))
            .AndAssert(result =>
            {
                var signal = Assert.IsType<OrderCancelledForStock>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, signal.Id);
                Assert.Equal(orderId, signal.OrderId);
                Assert.Equal([missingId, bootId], signal.RejectedProductIds);
                Assert.Equal(OrderAggregate.CancelledStatus, signal.OrderStatus);
                Assert.True(signal.Timestamp >= before);
                Assert.True(signal.Timestamp <= DateTime.UtcNow.AddSeconds(1));
                Assert.Equal(
                    StockDescription(missingId.ToString("D"), "Alpine boot"),
                    result.Aggregate.State.Description);

                var json = JsonSerializer.Serialize(signal);
                Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("1 Main", json, StringComparison.Ordinal);
                Assert.Contains(missingId.ToString("D"), json, StringComparison.Ordinal);
                Assert.Contains(bootId.ToString("D"), json, StringComparison.Ordinal);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Empty_rejected_list_still_cancels()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(Guid.NewGuid(), "Alpine boot", 120m, 1) };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .When(Cancel(orderId, null))
            .ThenEmittedEvent<OrderCancelledForStock>(e => e.RejectedProductIds.Count == 0)
            .AndAssert(result =>
            {
                Assert.Equal(OrderAggregate.CancelledStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(StockDescription(), result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(OrderAggregate.SubmittedStatus)]
    [InlineData(OrderAggregate.StockConfirmedStatus)]
    [InlineData("Paid")]
    [InlineData("Shipped")]
    [InlineData(OrderAggregate.CancelledStatus)]
    public async Task Cancel_from_any_other_status_writes_no_event(string status)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(
                orderId,
                buyerId,
                status,
                [new OrderLine(productId, "Alpine boot", 120m, 2)]))
            .Given(Started(orderId, buyerId))
            .When(Cancel(orderId, [productId]))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(status, result.Aggregate.State.OrderStatus);
                Assert.Equal(productId, result.Aggregate.State.OrderItems[0].ProductId);
                Assert.Equal(2, result.Aggregate.State.OrderItems[0].Units);
                Assert.Null(result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Second_cancel_after_cancelled_writes_no_event()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 2) };
        var cancelled = new OrderCancelledForStock(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            [productId],
            OrderAggregate.CancelledStatus);

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .Given(cancelled)
            .When(Cancel(orderId, [productId]))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(OrderAggregate.CancelledStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(StockDescription("Alpine boot"), result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Replay_restores_cancelled_for_stock()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 2) };
        var cancelled = new OrderCancelledForStock(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            [productId],
            OrderAggregate.CancelledStatus);

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .Given(cancelled)
            .When(Cancel(orderId, [productId]))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.CancelledStatus, state.OrderStatus);
                Assert.Equal(StockDescription("Alpine boot"), state.Description);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal(orderId, state.OrderId);
                var line = Assert.Single(state.OrderItems);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal(2, line.Units);
                Assert.Equal(120m, line.UnitPrice);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Cancel_rejects_an_unknown_order()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Cancel(orderId, [Guid.NewGuid()]))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.OrderNotFoundMessage, error.Message);
                Assert.Equal(OrderAggregate.OrderNotFoundTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal("", result.Aggregate.State.OrderStatus);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Cancel_rejects_an_empty_order_id()
    {
        await using var ctx = Context();

        await AggregateSpec
            .For<OrderAggregate>(ctx, Guid.NewGuid())
            .When(new CancelOrderForInsufficientStockCommand(Guid.NewGuid(), Guid.Empty, [Guid.NewGuid()]))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.OrderIdRequiredMessage, error.Message);
                Assert.Equal(OrderAggregate.OrderIdRequiredTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Cancel_rejects_an_empty_command_id()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(Guid.NewGuid(), "Alpine boot", 120m, 1) };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .When(new CancelOrderForInsufficientStockCommand(Guid.Empty, orderId, [lines[0].ProductId]))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.RequestIdRequiredMessage, error.Message);
                Assert.Equal(OrderAggregate.RequestIdRequiredTitle, error.Title);
                Assert.Equal(OrderAggregate.AwaitingValidationStatus, result.Aggregate.State.OrderStatus);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed),
            typeof(OrderCancelledForStock));

    private static CancelOrderForInsufficientStockCommand Cancel(Guid orderId, IReadOnlyList<Guid>? productIds) =>
        new(Guid.NewGuid(), orderId, productIds);

    private static string StockDescription(params string[] labels) =>
        $"The product items don't have stock: ({string.Join(", ", labels)}).";

    private static OrderPlaced Placed(
        Guid orderId,
        Guid buyerId,
        string status,
        IReadOnlyList<OrderLine> lines) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            buyerId,
            "Ada Lovelace",
            status,
            lines,
            "1 Main",
            "Seattle",
            "WA",
            "USA",
            "98101",
            1,
            "Ada Lovelace",
            "4444",
            new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());

    private static OrderStarted Started(Guid orderId, Guid buyerId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, buyerId);

    private static OrderAwaitingValidation Awaiting(
        Guid orderId,
        Guid buyerId,
        IReadOnlyList<OrderLine> lines)
    {
        var stock = new List<OrderStockItem>(lines.Count);
        foreach (var line in lines)
            stock.Add(new OrderStockItem(line.ProductId, line.Units));

        return new OrderAwaitingValidation(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            stock,
            buyerId);
    }
}
