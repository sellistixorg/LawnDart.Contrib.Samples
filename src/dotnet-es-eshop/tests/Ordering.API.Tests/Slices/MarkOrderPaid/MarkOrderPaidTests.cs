using System.Text.Json;
using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.MarkOrderPaid;

public class MarkOrderPaidTests
{
    [Fact]
    public async Task Stock_confirmed_moves_to_paid()
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
            .Given(Confirmed(orderId, buyerId, 265m))
            .When(Mark(orderId))
            .ThenEmittedTypes(typeof(OrderPaid))
            .ThenEmittedEvent<OrderPaid>(e => e.OrderId == orderId)
            .ThenAppendedOnlyTo($"Order:{orderId}")
            .AndAssert(result =>
            {
                var paid = Assert.IsType<OrderPaid>(Assert.Single(result.EmittedEvents));
                Assert.Equal(buyerId, paid.BuyerId);
                Assert.Equal(2, paid.OrderStockItems.Count);
                Assert.Equal(bootId, paid.OrderStockItems[0].ProductId);
                Assert.Equal(2, paid.OrderStockItems[0].Units);
                Assert.Equal(capId, paid.OrderStockItems[1].ProductId);
                Assert.Equal(1, paid.OrderStockItems[1].Units);

                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.PaidStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.PaidDescription, state.Description);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(2, state.OrderItems.Count);
                Assert.Equal("Alpine boot", state.OrderItems[0].ProductName);
                Assert.Equal(120m, state.OrderItems[0].UnitPrice);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Given_stock_confirmed_when_mark_order_paid_then_order_paid()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Confirmed(orderId, buyerId, 40m))
            .When(Mark(orderId))
            .ThenEmittedTypes(typeof(OrderPaid))
            .AndAssert(result =>
            {
                var paid = Assert.IsType<OrderPaid>(Assert.Single(result.EmittedEvents));
                Assert.Equal(orderId, paid.OrderId);
                Assert.Equal(buyerId, paid.BuyerId);
                Assert.Empty(paid.OrderStockItems);
                Assert.Equal(OrderAggregate.PaidStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(OrderAggregate.PaidDescription, result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Integration_event_carries_order_buyer_and_units()
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
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .Given(Confirmed(orderId, buyerId, 265m))
            .When(Mark(orderId))
            .AndAssert(result =>
            {
                var signal = Assert.IsType<OrderPaid>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, signal.Id);
                Assert.Equal(orderId, signal.OrderId);
                Assert.Equal(buyerId, signal.BuyerId);
                Assert.Equal(2, signal.OrderStockItems.Count);
                Assert.Equal(bootId, signal.OrderStockItems[0].ProductId);
                Assert.Equal(2, signal.OrderStockItems[0].Units);
                Assert.True(signal.Timestamp >= before);
                Assert.True(signal.Timestamp <= DateTime.UtcNow.AddSeconds(1));

                var json = JsonSerializer.Serialize(signal);
                Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Alpine boot", json, StringComparison.Ordinal);
                Assert.DoesNotContain("Paid", json, StringComparison.Ordinal);
                Assert.DoesNotContain("orderStatus", json, StringComparison.OrdinalIgnoreCase);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(OrderAggregate.SubmittedStatus)]
    [InlineData(OrderAggregate.AwaitingValidationStatus)]
    [InlineData("Shipped")]
    [InlineData("Cancelled")]
    public async Task Mark_from_any_other_status_writes_no_event(string status)
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
            .When(Mark(orderId))
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
    public async Task Second_mark_after_paid_writes_no_event()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 2) };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .Given(Confirmed(orderId, buyerId, 240m))
            .Given(new OrderPaid(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [new OrderStockItem(productId, 2)],
                buyerId))
            .When(Mark(orderId))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(OrderAggregate.PaidStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(OrderAggregate.PaidDescription, result.Aggregate.State.Description);
                var line = Assert.Single(result.Aggregate.State.OrderItems);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal(2, line.Units);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Replay_restores_paid()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 2) };
        var paid = new OrderPaid(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            [new OrderStockItem(productId, 2)],
            buyerId);

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Started(orderId, buyerId))
            .Given(Awaiting(orderId, buyerId, lines))
            .Given(Confirmed(orderId, buyerId, 240m))
            .Given(paid)
            .When(Mark(orderId))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.PaidStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.PaidDescription, state.Description);
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
    public async Task Mark_rejects_an_unknown_order()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Mark(orderId))
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
    public async Task Mark_rejects_an_empty_order_id()
    {
        await using var ctx = Context();

        await AggregateSpec
            .For<OrderAggregate>(ctx, Guid.NewGuid())
            .When(new MarkOrderPaidCommand(Guid.NewGuid(), Guid.Empty))
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
    public async Task Mark_rejects_an_empty_command_id()
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
            .Given(Confirmed(orderId, buyerId, 120m))
            .When(new MarkOrderPaidCommand(Guid.Empty, orderId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.RequestIdRequiredMessage, error.Message);
                Assert.Equal(OrderAggregate.RequestIdRequiredTitle, error.Title);
                Assert.Equal(OrderAggregate.StockConfirmedStatus, result.Aggregate.State.OrderStatus);
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
            typeof(OrderPaid));

    private static MarkOrderPaidCommand Mark(Guid orderId) =>
        new(Guid.NewGuid(), orderId);

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

    private static OrderStatusStockConfirmed Confirmed(Guid orderId, Guid buyerId, decimal total) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            buyerId,
            total,
            "Ada Lovelace");
}
