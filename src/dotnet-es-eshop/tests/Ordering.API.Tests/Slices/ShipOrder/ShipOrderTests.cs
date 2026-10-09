using System.Text.Json;
using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.ShipOrder;

public class ShipOrderTests
{
    [Fact]
    public async Task Given_order_paid_when_ship_order_then_order_shipped()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Paid(orderId, buyerId, productId, 2))
            .When(Ship(orderId))
            .ThenEmittedTypes(typeof(OrderShipped))
            .ThenEmittedEvent<OrderShipped>(e => e.OrderId == orderId)
            .ThenAppendedOnlyTo($"Order:{orderId}")
            .AndAssert(result =>
            {
                var shipped = Assert.IsType<OrderShipped>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, shipped.Id);
                Assert.Equal(orderId, shipped.OrderId);
                Assert.Equal(OrderAggregate.ShippedStatus, shipped.OrderStatus);
                Assert.Equal(OrderAggregate.ShippedDescription, shipped.Description);
                Assert.True(shipped.Timestamp >= before);
                Assert.True(shipped.Timestamp <= DateTime.UtcNow.AddSeconds(1));

                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.ShippedStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.ShippedDescription, state.Description);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal(orderId, state.OrderId);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Paid_order_keeps_lines_address_and_card()
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
            .Given(Paid(orderId, buyerId, bootId, 2))
            .When(Ship(orderId))
            .ThenEmittedTypes(typeof(OrderShipped))
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.ShippedStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.ShippedDescription, state.Description);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal("1 Main", state.Street);
                Assert.Equal("Seattle", state.City);
                Assert.Equal("WA", state.State);
                Assert.Equal("USA", state.Country);
                Assert.Equal("98101", state.ZipCode);
                Assert.Equal("4444", state.MaskedCardNumber);
                Assert.Equal(2, state.OrderItems.Count);
                Assert.Equal("Alpine boot", state.OrderItems[0].ProductName);
                Assert.Equal(120m, state.OrderItems[0].UnitPrice);
                Assert.Equal(2, state.OrderItems[0].Units);

                var shipped = Assert.IsType<OrderShipped>(Assert.Single(result.EmittedEvents));
                var json = JsonSerializer.Serialize(shipped);
                Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("1 Main", json, StringComparison.Ordinal);
                Assert.Contains(OrderAggregate.ShippedDescription, json, StringComparison.Ordinal);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Given_order_placed_only_when_ship_then_cannot_ship()
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
                OrderAggregate.SubmittedStatus,
                [new OrderLine(productId, "Alpine boot", 120m, 2)]))
            .When(Ship(orderId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.CannotShipOrderMessage, error.Message);
                Assert.Equal(OrderAggregate.CannotShipOrderTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(OrderAggregate.SubmittedStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(productId, result.Aggregate.State.OrderItems[0].ProductId);
                Assert.Null(result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(OrderAggregate.AwaitingValidationStatus)]
    [InlineData(OrderAggregate.StockConfirmedStatus)]
    [InlineData(OrderAggregate.ShippedStatus)]
    [InlineData(OrderAggregate.CancelledStatus)]
    public async Task Any_status_other_than_paid_is_rejected(string status)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, status, [new OrderLine(productId, "Alpine boot", 120m, 2)]))
            .When(Ship(orderId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.CannotShipOrderMessage, error.Message);
                Assert.Equal(OrderAggregate.CannotShipOrderTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(status, result.Aggregate.State.OrderStatus);
                Assert.Equal(productId, result.Aggregate.State.OrderItems[0].ProductId);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Second_ship_after_shipped_is_rejected()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 2) };
        var shipped = new OrderShipped(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            OrderAggregate.ShippedStatus,
            OrderAggregate.ShippedDescription);

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(Paid(orderId, buyerId, productId, 2))
            .Given(shipped)
            .When(Ship(orderId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.CannotShipOrderTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.ShippedStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.ShippedDescription, state.Description);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(buyerId, state.BuyerId);
                var line = Assert.Single(state.OrderItems);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal(2, line.Units);
                Assert.Equal(120m, line.UnitPrice);
                Assert.Equal("4444", state.MaskedCardNumber);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Ship_rejects_an_unknown_order()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Ship(orderId))
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
    public async Task Ship_rejects_an_empty_order_id()
    {
        await using var ctx = Context();

        await AggregateSpec
            .For<OrderAggregate>(ctx, Guid.NewGuid())
            .When(new ShipOrderCommand(Guid.NewGuid(), Guid.Empty))
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
    public async Task Ship_rejects_an_empty_command_id()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Paid(orderId, buyerId, Guid.NewGuid(), 1))
            .When(new ShipOrderCommand(Guid.Empty, orderId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.RequestIdRequiredMessage, error.Message);
                Assert.Equal(OrderAggregate.RequestIdRequiredTitle, error.Title);
                Assert.Equal(OrderAggregate.PaidStatus, result.Aggregate.State.OrderStatus);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderPaid),
            typeof(OrderShipped));

    private static ShipOrderCommand Ship(Guid orderId) =>
        new(Guid.NewGuid(), orderId);

    private static OrderPaid Paid(Guid orderId, Guid buyerId, Guid productId, int units) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            [new OrderStockItem(productId, units)],
            buyerId);

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
}
