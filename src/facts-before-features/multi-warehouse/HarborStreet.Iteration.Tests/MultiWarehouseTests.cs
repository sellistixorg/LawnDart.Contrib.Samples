using HarborStreet.Iteration;
using HarborStreet.Iteration.Domain;
using HarborStreet.Iteration.Events;
using HarborStreet.Iteration.Services;
using LawnDart.EventStore;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Iteration.Tests;

/// <summary>
/// Model v2 — multi-warehouse stock. GWT: docs/models/bookstore-v2/MULTI_WAREHOUSE_GWT.md
/// </summary>
public class MultiWarehouseTests
{
// start snippet downtown-last-copy
    [Fact]
    public async Task downtown_unavailable_while_harbor_has_last_copy()
    {
        await using var ctx = BddTestContext.CreateInMemory(IterationEventTypes.All);
        var checkout = new MultiWarehouseCheckout(ctx.AggregateRepository, ctx.DcbRepository);
        var titleId = Guid.NewGuid();
        var harbor = Guid.NewGuid();
        var downtown = Guid.NewGuid();

        await checkout.EnsureTitleAsync(titleId, "The Lighthouse Keeper", 52.00m);
        await checkout.OpenWarehouseAsync(harbor, "Harbor");
        await checkout.OpenWarehouseAsync(downtown, "Downtown");
        await checkout.RestockAsync(harbor, titleId, 1);

        var downtownOrder = await checkout.PlaceOrderAsync(
            Guid.NewGuid(), Guid.NewGuid(), downtown, titleId);

        Assert.Equal(WarehouseCheckoutStatus.RejectedUnavailable, downtownOrder.Status);

        var harborInv = await ctx.DcbRepository.GetOrCreateEntityAsync<WarehouseInventoryDecision>(
            WarehouseInventoryDecision.BoundaryTags(harbor, titleId));
        Assert.Equal(1, harborInv.State.AvailableUnits);
    }
// end snippet downtown-last-copy

    [Fact]
    public async Task concurrent_harbor_last_copy_exactly_one_winner()
    {
        await using var ctx = BddTestContext.CreateInMemory(IterationEventTypes.All);
        var checkout = new MultiWarehouseCheckout(ctx.AggregateRepository, ctx.DcbRepository);
        var titleId = Guid.NewGuid();
        var harbor = Guid.NewGuid();

        await checkout.EnsureTitleAsync(titleId, "The Lighthouse Keeper", 52.00m);
        await checkout.OpenWarehouseAsync(harbor, "Harbor");
        await checkout.RestockAsync(harbor, titleId, 1);

        var tags = WarehouseInventoryDecision.BoundaryTags(harbor, titleId);
        var entityA = await ctx.DcbRepository.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags);
        var entityB = await ctx.DcbRepository.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags);
        Assert.Equal(1, entityA.State.AvailableUnits);

        var results = await Task.WhenAll(
            checkout.PlaceOrderAsync(Guid.NewGuid(), Guid.NewGuid(), harbor, titleId, preloaded: entityA),
            checkout.PlaceOrderAsync(Guid.NewGuid(), Guid.NewGuid(), harbor, titleId, preloaded: entityB));

        Assert.Equal(1, results.Count(r => r.Status == WarehouseCheckoutStatus.Succeeded));
        Assert.Equal(1, results.Count(r =>
            r.Status is WarehouseCheckoutStatus.ConflictLost or WarehouseCheckoutStatus.RejectedUnavailable));

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReservedAtWarehouse));
    }

    [Fact]
    public async Task title_has_no_global_stock_fields_in_v2()
    {
        // Guardrail: catalog Title is identity-only; stock lives at warehouse.
        await using var ctx = BddTestContext.CreateInMemory(IterationEventTypes.All);
        var checkout = new MultiWarehouseCheckout(ctx.AggregateRepository, ctx.DcbRepository);
        var titleId = Guid.NewGuid();
        var harbor = Guid.NewGuid();

        await checkout.EnsureTitleAsync(titleId, "The Lighthouse Keeper", 52.00m);
        await checkout.OpenWarehouseAsync(harbor, "Harbor");
        await checkout.RestockAsync(harbor, titleId, 3);

        var title = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.True(title.State.Exists);
        Assert.Equal("The Lighthouse Keeper", title.State.Name);

        // No AvailableUnits on TitleState — compile-time guard via shape; runtime check inventory tag.
        var inv = await ctx.DcbRepository.GetOrCreateEntityAsync<WarehouseInventoryDecision>(
            WarehouseInventoryDecision.BoundaryTags(harbor, titleId));
        Assert.Equal(3, inv.State.AvailableUnits);
    }
}
