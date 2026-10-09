using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.UpdateCatalogItem;

public class UpdateCatalogItemTests
{
    [Fact]
    public async Task Update_without_price_change_emits_catalog_item_updated()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var newBrandId = Guid.NewGuid();
        var newTypeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, brandId, typeId))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "  Trail boot  ",
                120m,
                newBrandId,
                newTypeId,
                "  ",
                "  pack.png  "))
            .ThenEmittedTypes(typeof(CatalogItemUpdated))
            .ThenEmittedEvent<CatalogItemUpdated>(e =>
                e.ItemId == itemId
                && e.Name == "Trail boot"
                && e.Description == null
                && e.Price == 120m
                && e.BrandId == newBrandId
                && e.TypeId == newTypeId
                && e.PictureFileName == "pack.png")
            .ThenAppendedOnlyTo($"CatalogItem:{itemId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal("Trail boot", state.Name);
                Assert.Null(state.Description);
                Assert.Equal(120m, state.Price);
                Assert.Equal("pack.png", state.PictureFileName);
                Assert.Equal(newBrandId, state.BrandId);
                Assert.Equal(newTypeId, state.TypeId);
                Assert.Equal(50, state.AvailableStock);
                Assert.Equal(10, state.RestockThreshold);
                Assert.Equal(100, state.MaxStockThreshold);
                Assert.False(state.OnReorder);
                Assert.True(state.Exists);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Update_with_a_new_price_emits_updated_and_price_changed()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, brandId, typeId, 120m))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                150m,
                brandId,
                typeId,
                "High-ankle boot",
                "boot.png"))
            .ThenEmittedTypes(typeof(CatalogItemUpdated), typeof(CatalogItemPriceChanged))
            .ThenEmittedEvent<CatalogItemUpdated>(e =>
                e.ItemId == itemId
                && e.Name == "Alpine boot"
                && e.Price == 150m
                && e.BrandId == brandId
                && e.TypeId == typeId)
            .ThenEmittedEvent<CatalogItemPriceChanged>(e =>
                e.ItemId == itemId
                && e.OldPrice == 120m
                && e.NewPrice == 150m)
            .ThenAppendedOnlyTo($"CatalogItem:{itemId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(150m, state.Price);
                Assert.Equal("Alpine boot", state.Name);
                Assert.Equal(50, state.AvailableStock);
                Assert.Equal(10, state.RestockThreshold);
                Assert.Equal(100, state.MaxStockThreshold);
                Assert.True(state.Exists);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_an_unknown_item()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(CatalogItem.NotFoundMessage, error.Message);
                Assert.Equal(CatalogItem.NotFoundTitle, error.Title);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_an_item_that_was_removed()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid()))
            .Given(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(CatalogItem.NotFoundMessage, error.Message);
                Assert.Equal(CatalogItem.NotFoundTitle, error.Title);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_an_empty_item_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));

        await AggregateSpec
            .For<CatalogItem>(ctx, Guid.NewGuid())
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                Guid.Empty,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Item id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_a_blank_name()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid()))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "  ",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Item name is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_an_empty_brand_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid()))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.Empty,
                Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Brand id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_an_empty_type_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid()))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.Empty))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Type id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_a_negative_price()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemUpdated),
            typeof(CatalogItemPriceChanged));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid()))
            .When(new UpdateCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                -1m,
                Guid.NewGuid(),
                Guid.NewGuid()))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Price cannot be negative.", result.Exception!.Message))
            .RunAsync();
    }

    private static CatalogItemAdded AddedItem(
        Guid itemId,
        Guid brandId,
        Guid typeId,
        decimal price = 120m) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            itemId,
            "Alpine boot",
            "High-ankle boot",
            price,
            "boot.png",
            brandId,
            typeId,
            50,
            10,
            100);
}
