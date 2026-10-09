using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.AddCatalogItem;

public class AddCatalogItemTests
{
    [Fact]
    public async Task Add_item_emits_catalog_item_added()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                brandId,
                typeId,
                50,
                "High-ankle boot",
                "boot.png",
                10,
                100))
            .ThenEmittedEvent<CatalogItemAdded>(e =>
                e.ItemId == itemId
                && e.Name == "Alpine boot"
                && e.Description == "High-ankle boot"
                && e.Price == 120m
                && e.PictureFileName == "boot.png"
                && e.BrandId == brandId
                && e.TypeId == typeId
                && e.AvailableStock == 50
                && e.RestockThreshold == 10
                && e.MaxStockThreshold == 100)
            .ThenAppendedOnlyTo($"CatalogItem:{itemId}")
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_an_existing_item()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(new CatalogItemAdded(
                Guid.NewGuid(),
                DateTime.UtcNow,
                itemId,
                "Alpine boot",
                "High-ankle boot",
                120m,
                "boot.png",
                brandId,
                typeId,
                50,
                10,
                100))
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                brandId,
                typeId,
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(CatalogItem.AlreadyExistsMessage, error.Message);
                Assert.Equal(CatalogItem.AlreadyExistsTitle, error.Title);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_a_removed_item()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(new CatalogItemAdded(
                Guid.NewGuid(),
                DateTime.UtcNow,
                itemId,
                "Alpine boot",
                "High-ankle boot",
                120m,
                "boot.png",
                brandId,
                typeId,
                50,
                10,
                100))
            .Given(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId))
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                brandId,
                typeId,
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(CatalogItem.AlreadyExistsMessage, error.Message);
                Assert.Equal(CatalogItem.AlreadyExistsTitle, error.Title);
                Assert.True(result.Aggregate.State.Removed);
                Assert.False(result.Aggregate.State.Exists);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_an_empty_item_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));

        await AggregateSpec
            .For<CatalogItem>(ctx, Guid.NewGuid())
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                Guid.Empty,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Item id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_a_blank_name()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "  ",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Item name is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_an_empty_brand_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.Empty,
                Guid.NewGuid(),
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Brand id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_an_empty_type_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.Empty,
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Type id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_a_negative_price()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                -1m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                50))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Price cannot be negative.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_negative_stock()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                -1))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Available stock cannot be negative.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Add_item_rejects_stock_above_max_threshold()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(CatalogItemAdded));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                50,
                MaxStockThreshold: 10))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal(
                "Available stock cannot exceed max stock threshold.",
                result.Exception!.Message))
            .RunAsync();
    }
}
