using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.RemoveItemFromBasket;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Testing.Bdd;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.RemoveItemFromBasket;

public class RemoveItemFromBasketTests
{
    [Fact]
    public async Task Remove_item_drops_that_line_and_keeps_the_other()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Added(buyerId, otherProductId, "Wool cap", 25m, 4, "cap.png"))
            .When(Remove(buyerId, productId))
            .ThenEmittedEvent<BasketItemRemoved>(e =>
                e.BuyerId == buyerId
                && e.ProductId == productId)
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
                Assert.NotNull(result.Aggregate.State.LastUpdated);
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(otherProductId, line.ProductId);
                Assert.Equal("Wool cap", line.ProductName);
                Assert.Equal(25m, line.UnitPrice);
                Assert.Equal(4, line.Quantity);
                Assert.Equal("cap.png", line.PictureUrl);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Remove_last_line_leaves_an_empty_basket()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .When(Remove(buyerId, productId))
            .ThenEmittedEvent<BasketItemRemoved>(e =>
                e.BuyerId == buyerId
                && e.ProductId == productId)
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
                Assert.NotNull(result.Aggregate.State.LastUpdated);
                Assert.Empty(result.Aggregate.State.Items);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Re_adding_a_removed_product_starts_a_fresh_line()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Removed(buyerId, productId))
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                productId,
                "Trail shoe",
                80m,
                1,
                "shoe.png"))
            .ThenEmittedEvent<BasketItemAdded>(e =>
                e.BuyerId == buyerId
                && e.ProductId == productId
                && e.ProductName == "Trail shoe"
                && e.UnitPrice == 80m
                && e.Quantity == 1
                && e.PictureUrl == "shoe.png")
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal("Trail shoe", line.ProductName);
                Assert.Equal(80m, line.UnitPrice);
                Assert.Equal(1, line.Quantity);
                Assert.Equal("shoe.png", line.PictureUrl);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Remove_item_rejects_an_unknown_basket()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(Remove(buyerId, Guid.NewGuid()))
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
    public async Task Remove_item_rejects_a_product_that_is_not_in_the_basket()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .When(Remove(buyerId, Guid.NewGuid()))
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
    public async Task Remove_item_rejects_a_product_already_removed()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Removed(buyerId, productId))
            .When(Remove(buyerId, productId))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(BasketAggregate.ItemNotFoundMessage, error.Message);
                Assert.Equal(BasketAggregate.ItemNotFoundTitle, error.Title);
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
                Assert.Empty(result.Aggregate.State.Items);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Remove_item_rejects_an_empty_buyer_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));

        await AggregateSpec
            .For<BasketAggregate>(ctx, Guid.NewGuid())
            .When(Remove(Guid.Empty, Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Buyer id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Remove_item_rejects_an_empty_product_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketItemRemoved));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, Guid.NewGuid(), "Alpine boot", 120m, 2, "boot.png"))
            .When(Remove(buyerId, Guid.Empty))
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

    private static BasketItemRemoved Removed(Guid buyerId, Guid productId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, buyerId, productId);

    private static RemoveItemFromBasketCommand Remove(Guid buyerId, Guid productId) =>
        new(Guid.NewGuid(), buyerId, productId);
}
