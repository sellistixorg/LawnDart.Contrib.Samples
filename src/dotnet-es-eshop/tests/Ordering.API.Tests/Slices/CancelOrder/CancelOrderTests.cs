using System.Text.Json;
using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.CancelOrder;

public class CancelOrderTests
{
    [Fact]
    public async Task Submitted_order_cancels()
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
            .When(Cancel(orderId, "  Changed my mind  "))
            .ThenEmittedTypes(typeof(OrderCancelled))
            .ThenEmittedEvent<OrderCancelled>(e => e.OrderId == orderId)
            .ThenAppendedOnlyTo($"Order:{orderId}")
            .AndAssert(result =>
            {
                var cancelled = Assert.IsType<OrderCancelled>(Assert.Single(result.EmittedEvents));
                Assert.Equal("Changed my mind", cancelled.Reason);
                Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.CancelledStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.CancelledDescription, state.Description);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(2, state.OrderItems.Count);
                Assert.Equal("Alpine boot", state.OrderItems[0].ProductName);
                Assert.Equal(120m, state.OrderItems[0].UnitPrice);
                Assert.Equal("4444", state.MaskedCardNumber);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Event_carries_cancelled_status_and_reason()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(Guid.NewGuid(), "Alpine boot", 120m, 2) };
        var before = DateTime.UtcNow;

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .When(Cancel(orderId, "Buyer asked"))
            .AndAssert(result =>
            {
                var cancelled = Assert.IsType<OrderCancelled>(Assert.Single(result.EmittedEvents));
                Assert.NotEqual(Guid.Empty, cancelled.Id);
                Assert.Equal(orderId, cancelled.OrderId);
                Assert.Equal(OrderAggregate.CancelledStatus, cancelled.OrderStatus);
                Assert.Equal("Buyer asked", cancelled.Reason);
                Assert.True(cancelled.Timestamp >= before);
                Assert.True(cancelled.Timestamp <= DateTime.UtcNow.AddSeconds(1));

                var json = JsonSerializer.Serialize(cancelled);
                Assert.DoesNotContain("CardNumber", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("1 Main", json, StringComparison.Ordinal);
                Assert.Contains("Buyer asked", json, StringComparison.Ordinal);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_reason_is_stored_as_no_reason(string? reason)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, [new OrderLine(Guid.NewGuid(), "Alpine boot", 120m, 1)]))
            .When(Cancel(orderId, reason))
            .ThenEmittedEvent<OrderCancelled>(e => e.Reason is null)
            .AndAssert(result =>
            {
                Assert.Equal(OrderAggregate.CancelledStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(OrderAggregate.CancelledDescription, result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(OrderAggregate.AwaitingValidationStatus)]
    [InlineData(OrderAggregate.StockConfirmedStatus)]
    public async Task Open_status_cancels(string status)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(Guid.NewGuid(), "Alpine boot", 120m, 1) };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, status, lines))
            .When(Cancel(orderId, "Operator"))
            .ThenEmittedEvent<OrderCancelled>(e => e.OrderStatus == OrderAggregate.CancelledStatus)
            .AndAssert(result =>
            {
                Assert.Equal(OrderAggregate.CancelledStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(OrderAggregate.CancelledDescription, result.Aggregate.State.Description);
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(OrderAggregate.PaidStatus)]
    [InlineData(OrderAggregate.ShippedStatus)]
    public async Task Paid_or_shipped_is_rejected(string status)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, status, [new OrderLine(productId, "Alpine boot", 120m, 2)]))
            .When(Cancel(orderId, "Too late"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.CannotCancelOrderMessage, error.Message);
                Assert.Equal(OrderAggregate.CannotCancelOrderTitle, error.Title);
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(status, result.Aggregate.State.OrderStatus);
                Assert.Equal(productId, result.Aggregate.State.OrderItems[0].ProductId);
                Assert.Null(result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Already_cancelled_writes_no_event()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(Guid.NewGuid(), "Alpine boot", 120m, 1) };
        var cancelled = new OrderCancelled(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            OrderAggregate.CancelledStatus,
            "First reason");

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(cancelled)
            .When(Cancel(orderId, "Second reason"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(OrderAggregate.CancelledStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(OrderAggregate.CancelledDescription, result.Aggregate.State.Description);
                Assert.Equal("Ada Lovelace", result.Aggregate.State.Name);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Stock_cancellation_writes_no_second_event()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 1) };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(new OrderCancelledForStock(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [productId],
                OrderAggregate.CancelledStatus))
            .When(Cancel(orderId, "Shopper"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(OrderAggregate.CancelledStatus, result.Aggregate.State.OrderStatus);
                Assert.Equal(
                    "The product items don't have stock: (Alpine boot).",
                    result.Aggregate.State.Description);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Replay_restores_cancelled()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLine> { new(productId, "Alpine boot", 120m, 2) };
        var cancelled = new OrderCancelled(
            Guid.NewGuid(),
            DateTime.UtcNow,
            orderId,
            OrderAggregate.CancelledStatus,
            "Changed my mind");

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, lines))
            .Given(cancelled)
            .When(Cancel(orderId, "Again"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var state = result.Aggregate.State;
                Assert.Equal(OrderAggregate.CancelledStatus, state.OrderStatus);
                Assert.Equal(OrderAggregate.CancelledDescription, state.Description);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal(orderId, state.OrderId);
                var line = Assert.Single(state.OrderItems);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal(2, line.Units);
                Assert.Equal(120m, line.UnitPrice);
                Assert.Equal("4444", state.MaskedCardNumber);
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
            .When(Cancel(orderId, "Missing"))
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
            .When(new CancelOrderCommand(Guid.NewGuid(), Guid.Empty, "Missing"))
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

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(orderId, buyerId, OrderAggregate.SubmittedStatus, [new OrderLine(Guid.NewGuid(), "Alpine boot", 120m, 1)]))
            .When(new CancelOrderCommand(Guid.Empty, orderId, "Missing"))
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
            typeof(OrderAwaitingValidation),
            typeof(OrderStatusStockConfirmed),
            typeof(OrderCancelledForStock),
            typeof(OrderCancelled));

    private static CancelOrderCommand Cancel(Guid orderId, string? reason) =>
        new(Guid.NewGuid(), orderId, reason);

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
