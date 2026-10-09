using System.Text.Json;
using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.MarkOrderAwaitingValidation;

public class MarkOrderAwaitingValidationTests
{
    [Fact]
    public async Task Submitted_order_moves_to_awaiting_validation()
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
            .When(Mark(orderId))
            .ThenEmittedTypes(typeof(OrderAwaitingValidation))
            .ThenEmittedEvent<OrderAwaitingValidation>(e =>
                e.OrderId == orderId
                && e.BuyerId == buyerId
                && e.OrderStockItems.Count == 2)
            .ThenAppendedOnlyTo($"Order:{orderId}")
            .AndAssert(result =>
            {
                var signal = Assert.IsType<OrderAwaitingValidation>(result.EmittedEvents[0]);
                Assert.Equal(bootId, signal.OrderStockItems[0].ProductId);
                Assert.Equal(2, signal.OrderStockItems[0].Units);
                Assert.Equal(capId, signal.OrderStockItems[1].ProductId);
                Assert.Equal(1, signal.OrderStockItems[1].Units);

                var json = JsonSerializer.Serialize(signal);
                Assert.DoesNotContain("Alpine boot", json, StringComparison.Ordinal);
                Assert.DoesNotContain("Wool cap", json, StringComparison.Ordinal);
                Assert.DoesNotContain("unitPrice", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("productName", json, StringComparison.OrdinalIgnoreCase);

                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.AwaitingValidationStatus, state.OrderStatus);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal(2, state.OrderItems.Count);
                Assert.Equal("Alpine boot", state.OrderItems[0].ProductName);
                Assert.Equal(120m, state.OrderItems[0].UnitPrice);
                Assert.Null(state.Description);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(OrderAggregate.AwaitingValidationStatus)]
    [InlineData("StockConfirmed")]
    [InlineData("Paid")]
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
            })
            .RunAsync();
    }

    [Fact]
    public async Task Second_mark_after_awaiting_validation_writes_no_event()
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
            .Given(new OrderAwaitingValidation(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [new OrderStockItem(productId, 2)],
                buyerId))
            .When(Mark(orderId))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(OrderAggregate.AwaitingValidationStatus, result.Aggregate.State.OrderStatus);
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
            .When(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), Guid.Empty))
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

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(
                orderId,
                buyerId,
                OrderAggregate.SubmittedStatus,
                [new OrderLine(Guid.NewGuid(), "Alpine boot", 120m, 1)]))
            .Given(Started(orderId, buyerId))
            .When(new MarkOrderAwaitingValidationCommand(Guid.Empty, orderId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.RequestIdRequiredMessage, error.Message);
                Assert.Equal(OrderAggregate.RequestIdRequiredTitle, error.Title);
                Assert.Equal(OrderAggregate.SubmittedStatus, result.Aggregate.State.OrderStatus);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(OrderPlaced),
            typeof(OrderStarted),
            typeof(OrderAwaitingValidation));

    private static MarkOrderAwaitingValidationCommand Mark(Guid orderId) =>
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
}
