using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.RestockCatalogItem;

public class RestockCatalogItemTests
{
    [Fact]
    public async Task Restock_item_emits_catalog_item_restocked()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, brandId, typeId, availableStock: 50, maxStockThreshold: 100))
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 10))
            .ThenEmittedTypes(typeof(CatalogItemRestocked))
            .ThenEmittedEvent<CatalogItemRestocked>(e =>
                e.ItemId == itemId
                && e.QuantityAdded == 10
                && e.AvailableStock == 60)
            .ThenAppendedOnlyTo($"CatalogItem:{itemId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(itemId, state.ItemId);
                Assert.Equal("Alpine boot", state.Name);
                Assert.Equal(60, state.AvailableStock);
                Assert.Equal(100, state.MaxStockThreshold);
                Assert.Equal(10, state.RestockThreshold);
                Assert.False(state.OnReorder);
                Assert.True(state.Exists);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Restock_adds_only_the_units_that_fit_under_max_stock()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid(), availableStock: 50, maxStockThreshold: 100))
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 80))
            .ThenEmittedEvent<CatalogItemRestocked>(e =>
                e.QuantityAdded == 50
                && e.AvailableStock == 100)
            .AndAssert(result =>
            {
                Assert.Equal(100, result.Aggregate.State.AvailableStock);
                Assert.False(result.Aggregate.State.OnReorder);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Restock_with_no_max_stock_threshold_adds_the_full_quantity()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid(), availableStock: 50, maxStockThreshold: null))
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 80))
            .ThenEmittedEvent<CatalogItemRestocked>(e =>
                e.QuantityAdded == 80
                && e.AvailableStock == 130)
            .AndAssert(result => Assert.Equal(130, result.Aggregate.State.AvailableStock))
            .RunAsync();
    }

    [Fact]
    public async Task Restock_at_the_cap_records_zero_units_and_clears_on_reorder()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid(), availableStock: 100, maxStockThreshold: 100))
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 5))
            .ThenEmittedEvent<CatalogItemRestocked>(e =>
                e.QuantityAdded == 0
                && e.AvailableStock == 100)
            .AndAssert(result =>
            {
                Assert.Equal(100, result.Aggregate.State.AvailableStock);
                Assert.False(result.Aggregate.State.OnReorder);
            })
            .RunAsync();
    }

    [Fact]
    public void Restock_clears_on_reorder_when_it_was_true()
    {
        var itemId = Guid.NewGuid();
        var item = new CatalogItem();
        item.Handle(new AddCatalogItemCommand(
            Guid.NewGuid(),
            itemId,
            "Alpine boot",
            120m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            50,
            MaxStockThreshold: 100));
        item.State.OnReorder = true;

        item.Handle(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 5));

        Assert.False(item.State.OnReorder);
        Assert.Equal(55, item.State.AvailableStock);
    }

    [Fact]
    public async Task Restock_rejects_an_unknown_item()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 5))
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
    public async Task Restock_rejects_an_item_that_was_removed()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid(), availableStock: 50, maxStockThreshold: 100))
            .Given(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId))
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, 5))
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
    public async Task Restock_rejects_an_empty_item_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));

        await AggregateSpec
            .For<CatalogItem>(ctx, Guid.NewGuid())
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), Guid.Empty, 5))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Item id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Fact]
    public async Task Restock_rejects_a_negative_quantity()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRestocked));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, Guid.NewGuid(), Guid.NewGuid(), availableStock: 50, maxStockThreshold: 100))
            .When(new RestockCatalogItemCommand(Guid.NewGuid(), itemId, -1))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Quantity cannot be negative.", result.Exception!.Message))
            .RunAsync();
    }

    private static CatalogItemAdded AddedItem(
        Guid itemId,
        Guid brandId,
        Guid typeId,
        int availableStock,
        int? maxStockThreshold) =>
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
            availableStock,
            10,
            maxStockThreshold);
}
