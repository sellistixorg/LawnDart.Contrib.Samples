using LawnDart;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.ValidateOrderStock;

public class ValidateOrderStockTests
{
    [Fact]
    public async Task All_lines_in_stock_emit_order_stock_confirmed()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var exactId = Guid.NewGuid();
        var surplusId = Guid.NewGuid();
        await AddItem(ctx, exactId, "Alpine boot", availableStock: 4);
        await AddItem(ctx, surplusId, "Wool cap", availableStock: 9);

        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(Command(orderId, new StockLine(exactId, 4), new StockLine(surplusId, 2)));

        var recorded = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
        var confirmed = Assert.IsType<OrderStockConfirmed>(Assert.Single(recorded).Event);
        Assert.Equal(orderId, confirmed.OrderId);
        Assert.NotEqual(Guid.Empty, confirmed.Id);
        Assert.NotEqual(default, confirmed.Timestamp);
        var json = System.Text.Json.JsonSerializer.Serialize(confirmed);
        Assert.DoesNotContain("Alpine boot", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Wool cap", json, StringComparison.Ordinal);
        Assert.DoesNotContain("HasStock", json, StringComparison.Ordinal);
        Assert.DoesNotContain("price", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", json, StringComparison.OrdinalIgnoreCase);

        var check = await ctx.AggregateRepository.GetOrCreateAsync<OrderStockValidation>(orderId);
        Assert.True(check.State.Checked);
        Assert.True(check.State.Confirmed);
        Assert.Equal(orderId, check.State.OrderId);

        await AssertStockUnchanged(ctx, exactId, 4);
        await AssertStockUnchanged(ctx, surplusId, 9);
    }

    [Fact]
    public async Task One_line_short_emits_order_stock_rejected_with_each_flag()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var shortId = Guid.NewGuid();
        var okId = Guid.NewGuid();
        await AddItem(ctx, shortId, "Alpine boot", availableStock: 2);
        await AddItem(ctx, okId, "Wool cap", availableStock: 5);

        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(Command(orderId, new StockLine(shortId, 3), new StockLine(okId, 1)));

        var recorded = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
        var rejected = Assert.IsType<OrderStockRejected>(Assert.Single(recorded).Event);
        Assert.Equal(orderId, rejected.OrderId);
        Assert.Equal(2, rejected.ConfirmedOrderStockItems.Count);
        Assert.Equal(shortId, rejected.ConfirmedOrderStockItems[0].ProductId);
        Assert.False(rejected.ConfirmedOrderStockItems[0].HasStock);
        Assert.Equal(okId, rejected.ConfirmedOrderStockItems[1].ProductId);
        Assert.True(rejected.ConfirmedOrderStockItems[1].HasStock);
        var json = System.Text.Json.JsonSerializer.Serialize(rejected);
        Assert.Contains(shortId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(okId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasStock", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpine boot", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Wool cap", json, StringComparison.Ordinal);
        Assert.DoesNotContain("unitPrice", json, StringComparison.OrdinalIgnoreCase);

        var check = await ctx.AggregateRepository.GetOrCreateAsync<OrderStockValidation>(orderId);
        Assert.True(check.State.Checked);
        Assert.False(check.State.Confirmed);
        await AssertStockUnchanged(ctx, shortId, 2);
        await AssertStockUnchanged(ctx, okId, 5);
    }

    [Fact]
    public async Task Unknown_item_counts_as_no_stock()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var knownId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        await AddItem(ctx, knownId, "Alpine boot", availableStock: 8);

        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(Command(orderId, new StockLine(knownId, 1), new StockLine(unknownId, 1)));

        var recorded = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
        var rejected = Assert.IsType<OrderStockRejected>(Assert.Single(recorded).Event);
        Assert.Equal(knownId, rejected.ConfirmedOrderStockItems[0].ProductId);
        Assert.True(rejected.ConfirmedOrderStockItems[0].HasStock);
        Assert.Equal(unknownId, rejected.ConfirmedOrderStockItems[1].ProductId);
        Assert.False(rejected.ConfirmedOrderStockItems[1].HasStock);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync($"CatalogItem:{unknownId}"));
        await AssertStockUnchanged(ctx, knownId, 8);
    }

    [Fact]
    public async Task Removed_item_counts_as_no_stock()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await AddItem(ctx, itemId, "Alpine boot", availableStock: 8);
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        await ctx.AggregateRepository.HandleCommandAsync(
            item,
            new RemoveCatalogItemCommand(Guid.NewGuid(), itemId));

        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(Command(orderId, new StockLine(itemId, 1)));

        var recorded = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
        var rejected = Assert.IsType<OrderStockRejected>(Assert.Single(recorded).Event);
        var flag = Assert.Single(rejected.ConfirmedOrderStockItems);
        Assert.Equal(itemId, flag.ProductId);
        Assert.False(flag.HasStock);

        var reread = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        Assert.False(reread.State.Exists);
        Assert.True(reread.State.Removed);
        Assert.Equal(8, reread.State.AvailableStock);
        var itemEvents = await ctx.EventStore.ReadStreamAsync($"CatalogItem:{itemId}");
        Assert.IsType<CatalogItemAdded>(itemEvents[0].Event);
        Assert.IsType<CatalogItemRemoved>(itemEvents[1].Event);
        Assert.Equal(2, itemEvents.Count);
    }

    [Fact]
    public async Task Repeat_writes_nothing_and_does_not_fail()
    {
        await using var ctx = Context();
        var confirmedOrderId = Guid.NewGuid();
        var rejectedOrderId = Guid.NewGuid();
        var inStockId = Guid.NewGuid();
        var shortId = Guid.NewGuid();
        await AddItem(ctx, inStockId, "Alpine boot", availableStock: 5);
        await AddItem(ctx, shortId, "Wool cap", availableStock: 1);
        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);

        var confirm = Command(confirmedOrderId, new StockLine(inStockId, 2));
        await handler.HandleAsync(confirm);
        var first = Assert.IsType<OrderStockConfirmed>(
            Assert.Single(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(confirmedOrderId))).Event);
        await handler.HandleAsync(confirm);
        await handler.HandleAsync(Command(confirmedOrderId, new StockLine(inStockId, 100)));
        var again = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(confirmedOrderId));
        var stored = Assert.IsType<OrderStockConfirmed>(Assert.Single(again).Event);
        Assert.Equal(first.Id, stored.Id);

        var reject = Command(rejectedOrderId, new StockLine(shortId, 4));
        await handler.HandleAsync(reject);
        var rejected = Assert.IsType<OrderStockRejected>(
            Assert.Single(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(rejectedOrderId))).Event);
        await handler.HandleAsync(reject);
        var rejectedAgain = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(rejectedOrderId));
        Assert.Equal(rejected.Id, Assert.IsType<OrderStockRejected>(Assert.Single(rejectedAgain).Event).Id);
        await AssertStockUnchanged(ctx, inStockId, 5);
        await AssertStockUnchanged(ctx, shortId, 1);
    }

    [Fact]
    public async Task Empty_input_writes_nothing()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await AddItem(ctx, itemId, "Alpine boot", availableStock: 6);
        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);

        var empty = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new ValidateOrderStockCommand(Guid.NewGuid(), orderId, [])));
        AssertError(empty, OrderStockValidation.ItemsRequiredMessage, OrderStockValidation.ItemsRequiredTitle);

        var missing = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new ValidateOrderStockCommand(Guid.NewGuid(), orderId, null!)));
        AssertError(missing, OrderStockValidation.ItemsRequiredMessage, OrderStockValidation.ItemsRequiredTitle);

        var noOrder = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(Command(Guid.Empty, new StockLine(itemId, 1))));
        AssertError(noOrder, OrderStockValidation.OrderIdRequiredMessage, OrderStockValidation.OrderIdRequiredTitle);

        var noCommand = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new ValidateOrderStockCommand(Guid.Empty, orderId, [new StockLine(itemId, 1)])));
        AssertError(noCommand, OrderStockValidation.CommandIdRequiredMessage, OrderStockValidation.CommandIdRequiredTitle);

        var noProduct = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(Command(orderId, new StockLine(Guid.Empty, 1))));
        AssertError(noProduct, OrderStockValidation.ProductIdRequiredMessage, OrderStockValidation.ProductIdRequiredTitle);

        var noUnits = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(Command(orderId, new StockLine(itemId, 0))));
        AssertError(noUnits, OrderStockValidation.UnitsRequiredMessage, OrderStockValidation.UnitsInvalidTitle);

        Assert.Empty(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId)));
        Assert.Empty(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(Guid.Empty)));
        await AssertStockUnchanged(ctx, itemId, 6);
    }

    [Fact]
    public async Task Each_line_is_checked_against_full_available_stock()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await AddItem(ctx, itemId, "Alpine boot", availableStock: 5);

        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(Command(orderId, new StockLine(itemId, 4), new StockLine(itemId, 4)));

        var recorded = await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
        var rejected = Assert.IsType<OrderStockRejected>(Assert.Single(recorded).Event);
        var flag = Assert.Single(rejected.ConfirmedOrderStockItems);
        Assert.Equal(itemId, flag.ProductId);
        Assert.False(flag.HasStock);
        await AssertStockUnchanged(ctx, itemId, 5);
    }

    [Fact]
    public async Task Missing_checked_lines_is_a_programmer_error()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var check = await ctx.AggregateRepository.GetOrCreateAsync<OrderStockValidation>(orderId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.AggregateRepository.HandleCommandAsync(
                check,
                new ValidateOrderStockCommand(Guid.NewGuid(), orderId, [new StockLine(Guid.NewGuid(), 1)])));

        Assert.Equal(
            "Confirmed order stock items must be set before the result is stored.",
            error.Message);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(orderId)));
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(CatalogItemRemoved),
            typeof(OrderStockConfirmed),
            typeof(OrderStockRejected));

    private static void AssertError(DomainException error, string message, string title)
    {
        Assert.Equal(message, error.Message);
        Assert.Equal(title, error.Title);
    }

    private static ValidateOrderStockCommand Command(Guid orderId, params StockLine[] lines) =>
        new(Guid.NewGuid(), orderId, lines);

    private static async Task AddItem(BddTestContext ctx, Guid itemId, string name, int availableStock)
    {
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        await ctx.AggregateRepository.HandleCommandAsync(
            item,
            new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                name,
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                availableStock,
                "High-ankle boot",
                "boot.png",
                10,
                100));
    }

    private static async Task AssertStockUnchanged(BddTestContext ctx, Guid itemId, int availableStock)
    {
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        Assert.Equal(availableStock, item.State.AvailableStock);
        Assert.True(item.State.Exists);
        Assert.Single(await ctx.EventStore.ReadStreamAsync($"CatalogItem:{itemId}"));
    }
}
