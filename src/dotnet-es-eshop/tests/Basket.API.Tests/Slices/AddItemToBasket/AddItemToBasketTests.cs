using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Testing.Bdd;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.AddItemToBasket;

public class AddItemToBasketTests
{
    [Fact]
    public async Task Add_item_to_an_empty_basket_emits_basket_item_added()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                productId,
                "Alpine boot",
                120m,
                2,
                "boot.png"))
            .ThenEmittedEvent<BasketItemAdded>(e =>
                e.BuyerId == buyerId
                && e.ProductId == productId
                && e.ProductName == "Alpine boot"
                && e.UnitPrice == 120m
                && e.Quantity == 2
                && e.PictureUrl == "boot.png")
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                var line = Assert.Single(state.Items);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.NotNull(state.LastUpdated);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal("Alpine boot", line.ProductName);
                Assert.Equal(120m, line.UnitPrice);
                Assert.Equal(2, line.Quantity);
                Assert.Equal("boot.png", line.PictureUrl);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Add_same_product_twice_sums_the_quantity()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                productId,
                "Other name",
                40m,
                3,
                "other.png"))
            .ThenEmittedEvent<BasketItemAdded>(e =>
                e.ProductId == productId
                && e.ProductName == "Alpine boot"
                && e.UnitPrice == 120m
                && e.Quantity == 5
                && e.PictureUrl == "boot.png")
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(productId, line.ProductId);
                Assert.Equal("Alpine boot", line.ProductName);
                Assert.Equal(120m, line.UnitPrice);
                Assert.Equal(5, line.Quantity);
                Assert.Equal("boot.png", line.PictureUrl);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Add_two_different_products_keeps_both_lines()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, firstProductId, "Alpine boot", 120m, 1, "boot.png"))
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                secondProductId,
                "Wool cap",
                25m,
                4,
                "cap.png"))
            .ThenEmittedEvent<BasketItemAdded>(e =>
                e.ProductId == secondProductId
                && e.ProductName == "Wool cap"
                && e.UnitPrice == 25m
                && e.Quantity == 4
                && e.PictureUrl == "cap.png")
            .ThenAppendedOnlyTo($"Basket:{buyerId}")
            .AndAssert(result =>
            {
                Assert.Equal(2, result.Aggregate.State.Items.Count);
                Assert.Contains(
                    result.Aggregate.State.Items,
                    line => line.ProductId == firstProductId && line.Quantity == 1);
                Assert.Contains(
                    result.Aggregate.State.Items,
                    line => line.ProductId == secondProductId
                        && line.ProductName == "Wool cap"
                        && line.Quantity == 4);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Add_same_product_rejects_a_quantity_that_overflows()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, int.MaxValue, "boot.png"))
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                productId,
                "Alpine boot",
                120m,
                1))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(BasketAggregate.QuantityInvalidMessage, error.Message);
                Assert.Equal(BasketAggregate.QuantityInvalidTitle, error.Title);
                var line = Assert.Single(result.Aggregate.State.Items);
                Assert.Equal(int.MaxValue, line.Quantity);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_an_empty_buyer_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));

        await AggregateSpec
            .For<BasketAggregate>(ctx, Guid.NewGuid())
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                Guid.Empty,
                Guid.NewGuid(),
                "Alpine boot",
                120m,
                1))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Buyer id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_an_empty_product_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                Guid.Empty,
                "Alpine boot",
                120m,
                1))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Product id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_a_negative_unit_price()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                Guid.NewGuid(),
                "Alpine boot",
                -1m,
                1))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Unit price cannot be negative.", result.Exception!.Message))
            .RunAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Add_item_rejects_a_quantity_below_one(int quantity)
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(BasketItemAdded));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                Guid.NewGuid(),
                "Alpine boot",
                120m,
                quantity))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(BasketAggregate.QuantityInvalidMessage, error.Message);
                Assert.Equal(BasketAggregate.QuantityInvalidTitle, error.Title);
                Assert.Empty(result.Aggregate.State.Items);
            })
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
