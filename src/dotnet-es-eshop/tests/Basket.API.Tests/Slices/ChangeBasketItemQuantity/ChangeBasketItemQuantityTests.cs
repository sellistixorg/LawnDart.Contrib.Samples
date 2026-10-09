using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.ChangeBasketItemQuantity;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Testing.Bdd;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.ChangeBasketItemQuantity;

public class ChangeBasketItemQuantityTests
{
    [Fact]
    public async Task Change_quantity_replaces_the_line_quantity()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Added(buyerId, otherProductId, "Wool cap", 25m, 4, "cap.png"))
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, productId, 7))
            .ThenEmittedEvent<BasketItemQuantityChanged>(e =>
                e.BuyerId == buyerId
                && e.ProductId == productId
                && e.OldQuantity == 2
                && e.NewQuantity == 7)
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
                Assert.NotNull(result.Aggregate.State.LastUpdated);
                Assert.Contains(
                    result.Aggregate.State.Items,
                    line => line.ProductId == productId
                        && line.ProductName == "Alpine boot"
                        && line.UnitPrice == 120m
                        && line.Quantity == 7
                        && line.PictureUrl == "boot.png");
                Assert.Contains(
                    result.Aggregate.State.Items,
                    line => line.ProductId == otherProductId
                        && line.ProductName == "Wool cap"
                        && line.UnitPrice == 25m
                        && line.Quantity == 4
                        && line.PictureUrl == "cap.png");
            })
            .RunAsync();
    }

    [Fact]
    public async Task Change_quantity_writes_no_event_when_the_quantity_is_unchanged()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, productId, 2))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal("Alpine boot", line.ProductName);
                Assert.Equal(120m, line.UnitPrice);
                Assert.Equal(2, line.Quantity);
                Assert.Equal("boot.png", line.PictureUrl);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Change_quantity_can_set_int_max_without_adding()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, int.MaxValue, "boot.png"))
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, productId, 1))
            .ThenEmittedEvent<BasketItemQuantityChanged>(e =>
                e.OldQuantity == int.MaxValue
                && e.NewQuantity == 1)
            .AndAssert(result =>
            {
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(1, line.Quantity);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Change_quantity_rejects_a_quantity_below_one(int quantity)
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, productId, quantity))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(BasketAggregate.QuantityInvalidMessage, error.Message);
                Assert.Equal(BasketAggregate.QuantityInvalidTitle, error.Title);
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(2, line.Quantity);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Change_quantity_rejects_an_unknown_basket()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, Guid.NewGuid(), 2))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(BasketAggregate.NotFoundMessage, error.Message);
                Assert.Equal(BasketAggregate.NotFoundTitle, error.Title);
                Assert.Empty(result.Aggregate.State.Items);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Change_quantity_rejects_a_product_that_is_not_in_the_basket()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, Guid.NewGuid(), 3))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(BasketAggregate.ItemNotFoundMessage, error.Message);
                Assert.Equal(BasketAggregate.ItemNotFoundTitle, error.Title);
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal(2, line.Quantity);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Change_quantity_rejects_an_empty_buyer_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));

        await AggregateSpec
            .For<BasketAggregate>(ctx, Guid.NewGuid())
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 2))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Buyer id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Change_quantity_rejects_an_empty_product_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemQuantityChanged));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, Guid.NewGuid(), "Alpine boot", 120m, 2, "boot.png"))
            .When(new ChangeBasketItemQuantityCommand(Guid.NewGuid(), buyerId, Guid.Empty, 2))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Product id is required.", result.Exception!.Message))
            .RunAsync();
    }

    private static BasketItemAdded Added(
        Guid buyerId,
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity,
        string? pictureUrl) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            buyerId,
            productId,
            productName,
            unitPrice,
            quantity,
            pictureUrl);
}
