using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.DeductCatalogStock;

public class DeductCatalogStockTests
{
    [Fact]
    public async Task Deduct_item_emits_catalog_stock_deducted()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, availableStock: 10))
            .When(Deduct(orderId, itemId, 3))
            .ThenEmittedTypes(typeof(CatalogStockDeducted))
            .ThenEmittedEvent<CatalogStockDeducted>(e =>
                e.OrderId == orderId
                && e.Deductions.Count == 1
                && e.Deductions[0].ItemId == itemId
                && e.Deductions[0].UnitsRemoved == 3)
            .ThenAppendedOnlyTo($"CatalogItem:{itemId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(itemId, state.ItemId);
                Assert.Equal("Alpine boot", state.Name);
                Assert.Equal(7, state.AvailableStock);
                Assert.True(state.Exists);
                Assert.False(state.Removed);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Deduct_removes_two_when_five_are_requested_and_two_are_on_hand()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, availableStock: 2))
            .When(Deduct(orderId, itemId, 5))
            .ThenEmittedEvent<CatalogStockDeducted>(e =>
                e.OrderId == orderId
                && e.Deductions.Count == 1
                && e.Deductions[0].ItemId == itemId
                && e.Deductions[0].UnitsRemoved == 2)
            .AndAssert(result => Assert.Equal(0, result.Aggregate.State.AvailableStock))
            .RunAsync();
    }

    [Fact]
    public async Task Deduct_rejects_when_available_stock_is_zero()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, availableStock: 0))
            .When(Deduct(Guid.NewGuid(), itemId, 1))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(CatalogItem.OutOfStockMessage, error.Message);
                Assert.Equal(CatalogItem.OutOfStockTitle, error.Title);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Deduct_rejects_a_non_positive_quantity(int units)
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, availableStock: 5))
            .When(Deduct(Guid.NewGuid(), itemId, units))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal("Item units desired should be greater than zero.", result.Exception!.Message))
            .RunAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Deduct_checks_zero_stock_before_quantity(int units)
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();
        await AddItem(ctx, itemId, availableStock: 0);

        var handler = new DeductCatalogStockHandler(ctx.AggregateRepository);
        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            Deduct(Guid.NewGuid(), itemId, units)));

        Assert.Equal(CatalogItem.OutOfStockMessage, error.Message);
        Assert.Equal(CatalogItem.OutOfStockTitle, error.Title);
        Assert.Empty(await DeductedEvents(ctx, itemId));
    }

    [Fact]
    public async Task Deduct_rejects_zero_units_on_an_empty_item_as_out_of_stock()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();

        await AggregateSpec
            .For<CatalogItem>(ctx, itemId)
            .Given(AddedItem(itemId, availableStock: 0))
            .When(Deduct(Guid.NewGuid(), itemId, 0))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(CatalogItem.OutOfStockMessage, error.Message);
                Assert.Equal(CatalogItem.OutOfStockTitle, error.Title);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Deduct_skips_an_unknown_item_and_still_deducts_other_lines()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var knownId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await AddItem(ctx, knownId, availableStock: 10);

        var handler = new DeductCatalogStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new DeductCatalogStockCommand(
            Guid.NewGuid(),
            orderId,
            [
                new OrderStockItem(unknownId, 4),
                new OrderStockItem(knownId, 3)
            ]));

        var known = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(knownId);
        Assert.Equal(7, known.State.AvailableStock);
        var deducted = await DeductedEvents(ctx, knownId);
        var stored = Assert.Single(deducted);
        Assert.Equal(orderId, stored.OrderId);
        var deduction = Assert.Single(stored.Deductions);
        Assert.Equal(knownId, deduction.ItemId);
        Assert.Equal(3, deduction.UnitsRemoved);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync($"CatalogItem:{unknownId}"));
    }

    [Fact]
    public async Task Deduct_combines_duplicate_lines_for_the_same_item()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await AddItem(ctx, itemId, availableStock: 10);

        var handler = new DeductCatalogStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new DeductCatalogStockCommand(
            Guid.NewGuid(),
            orderId,
            [
                new OrderStockItem(itemId, 3),
                new OrderStockItem(itemId, 4)
            ]));

        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        Assert.Equal(3, item.State.AvailableStock);

        var deducted = await DeductedEvents(ctx, itemId);
        var stored = Assert.Single(deducted);
        Assert.Equal(orderId, stored.OrderId);
        var deduction = Assert.Single(stored.Deductions);
        Assert.Equal(itemId, deduction.ItemId);
        Assert.Equal(7, deduction.UnitsRemoved);
    }

    [Fact]
    public async Task Deduct_rejects_a_non_positive_line_even_when_another_line_for_the_item_is_positive()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var itemId = Guid.NewGuid();
        await AddItem(ctx, itemId, availableStock: 10);

        var handler = new DeductCatalogStockHandler(ctx.AggregateRepository);
        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new DeductCatalogStockCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [
                    new OrderStockItem(itemId, 5),
                    new OrderStockItem(itemId, -3)
                ])));

        Assert.Equal("Item units desired should be greater than zero.", error.Message);
        Assert.Empty(await DeductedEvents(ctx, itemId));
    }

    [Fact]
    public async Task Deduct_leaves_earlier_stock_unchanged_when_a_later_line_fails()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogStockDeducted));
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await AddItem(ctx, firstId, availableStock: 10);
        await AddItem(ctx, secondId, availableStock: 0);
        var firstBefore = (await ctx.EventStore.ReadStreamAsync($"CatalogItem:{firstId}")).Count;
        var secondBefore = (await ctx.EventStore.ReadStreamAsync($"CatalogItem:{secondId}")).Count;

        var handler = new DeductCatalogStockHandler(ctx.AggregateRepository);
        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new DeductCatalogStockCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [
                    new OrderStockItem(firstId, 3),
                    new OrderStockItem(secondId, 1)
                ])));

        Assert.Equal(CatalogItem.OutOfStockMessage, error.Message);
        Assert.Equal(CatalogItem.OutOfStockTitle, error.Title);

        var first = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(firstId);
        Assert.Equal(10, first.State.AvailableStock);
        Assert.Equal(firstBefore, (await ctx.EventStore.ReadStreamAsync($"CatalogItem:{firstId}")).Count);
        Assert.Equal(secondBefore, (await ctx.EventStore.ReadStreamAsync($"CatalogItem:{secondId}")).Count);
        Assert.Empty(await DeductedEvents(ctx, firstId));
        Assert.Empty(await DeductedEvents(ctx, secondId));
    }

    [Fact]
    public async Task Deduct_skips_a_removed_item_and_still_deducts_other_lines()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved),
            typeof(CatalogStockDeducted));
        var removedId = Guid.NewGuid();
        var knownId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await AddItem(ctx, removedId, availableStock: 8);
        await AddItem(ctx, knownId, availableStock: 10);
        var removed = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(removedId);
        await ctx.AggregateRepository.HandleCommandAsync(
            removed,
            new RemoveCatalogItemCommand(Guid.NewGuid(), removedId));

        var handler = new DeductCatalogStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new DeductCatalogStockCommand(
            Guid.NewGuid(),
            orderId,
            [
                new OrderStockItem(removedId, 1),
                new OrderStockItem(knownId, 2)
            ]));

        var reread = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(removedId);
        Assert.False(reread.State.Exists);
        Assert.True(reread.State.Removed);
        Assert.Equal(8, reread.State.AvailableStock);
        Assert.Empty(await DeductedEvents(ctx, removedId));

        var known = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(knownId);
        Assert.Equal(8, known.State.AvailableStock);
        var deducted = await DeductedEvents(ctx, knownId);
        var stored = Assert.Single(deducted);
        Assert.Equal(orderId, stored.OrderId);
        var deduction = Assert.Single(stored.Deductions);
        Assert.Equal(2, deduction.UnitsRemoved);
    }

    private static CatalogItemAdded AddedItem(Guid itemId, int availableStock) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            itemId,
            "Alpine boot",
            "High-ankle boot",
            120m,
            "boot.png",
            Guid.NewGuid(),
            Guid.NewGuid(),
            availableStock,
            10,
            100);

    private static DeductCatalogStockCommand Deduct(Guid orderId, Guid itemId, int units) =>
        new(Guid.NewGuid(), orderId, [new OrderStockItem(itemId, units)]);

    private static async Task AddItem(BddTestContext ctx, Guid itemId, int availableStock)
    {
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        await ctx.AggregateRepository.HandleCommandAsync(
            item,
            new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                availableStock,
                "High-ankle boot",
                "boot.png",
                10,
                100));
    }

    private static async Task<IReadOnlyList<CatalogStockDeducted>> DeductedEvents(
        BddTestContext ctx,
        Guid itemId)
    {
        var events = await ctx.EventStore.ReadStreamAsync($"CatalogItem:{itemId}");
        var deducted = new List<CatalogStockDeducted>();
        foreach (var sequenced in events)
        {
            if (sequenced.Event is CatalogStockDeducted stockDeducted)
                deducted.Add(stockDeducted);
        }

        return deducted;
    }
}
