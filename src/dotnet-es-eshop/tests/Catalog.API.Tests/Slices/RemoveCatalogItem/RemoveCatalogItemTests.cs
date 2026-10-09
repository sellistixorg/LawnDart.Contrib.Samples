using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.RemoveCatalogItem;

public class RemoveCatalogItemTests
{
    [Fact]
    public async Task Remove_item_emits_catalog_item_removed()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, brandId, typeId))
            .When(new RemoveCatalogItemCommand(Guid.NewGuid(), itemId))
            .ThenEmittedTypes(typeof(CatalogItemRemoved))
            .ThenEmittedEvent<CatalogItemRemoved>(e => e.ItemId == itemId)
            .ThenAppendedOnlyTo($"CatalogItem:{itemId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(itemId, state.ItemId);
                Assert.Equal("Alpine boot", state.Name);
                Assert.Equal(120m, state.Price);
                Assert.Equal(50, state.AvailableStock);
                Assert.False(state.Exists);
                Assert.True(state.Removed);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Remove_rejects_an_unknown_item()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new RemoveCatalogItemCommand(Guid.NewGuid(), itemId))
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
    public async Task Remove_rejects_an_item_that_was_already_removed()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid()))
            .Given(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId))
            .When(new RemoveCatalogItemCommand(Guid.NewGuid(), itemId))
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
    public async Task Remove_rejects_an_empty_item_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved));

        await AggregateSpec
            .For<CatalogItem>(ctx, Guid.NewGuid())
            .When(new RemoveCatalogItemCommand(Guid.NewGuid(), Guid.Empty))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Item id is required.", result.Exception!.Message))
            .RunAsync();
    }

    private static CatalogItemAdded AddedItem(Guid itemId, Guid brandId, Guid typeId) =>
        new(
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
            100);
}
