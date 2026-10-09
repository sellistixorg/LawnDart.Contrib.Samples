using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Testing.Bdd;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.ClearBasket;

public class ClearBasketTests
{
    [Fact]
    public async Task Clear_basket_removes_every_line()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketCleared));
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, bootId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Added(buyerId, capId, "Wool cap", 25m, 4, "cap.png"))
            .When(Clear(buyerId, "UserCleared"))
            .ThenEmittedEvent<BasketCleared>(e =>
                e.BuyerId == buyerId
                && e.Reason == "UserCleared")
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
    public async Task Add_after_clear_starts_fresh_lines()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketCleared));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Cleared(buyerId, "UserCleared"))
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
    public async Task Clear_on_an_already_empty_basket_writes_no_event()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketCleared));
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .Given(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"))
            .Given(Cleared(buyerId, "UserCleared"))
            .When(Clear(buyerId, "UserCleared"))
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
                Assert.Empty(result.Aggregate.State.Items);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Clear_basket_rejects_an_unknown_basket()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketCleared));
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<BasketAggregate>(ctx, buyerId)
            .When(Clear(buyerId, "UserCleared"))
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
    public async Task Clear_basket_rejects_an_empty_buyer_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(BasketItemAdded),
            typeof(BasketCleared));

        await AggregateSpec
            .For<BasketAggregate>(ctx, Guid.NewGuid())
            .When(Clear(Guid.Empty, "UserCleared"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Buyer id is required.", result.Exception!.Message))
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

    private static BasketCleared Cleared(Guid buyerId, string? reason) =>
        new(Guid.NewGuid(), DateTime.UtcNow, buyerId, reason);

    private static ClearBasketCommand Clear(Guid buyerId, string? reason) =>
        new(Guid.NewGuid(), buyerId, reason);
}
