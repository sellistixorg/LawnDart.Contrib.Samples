using HarborStreet.Checkout.Aggregates;
using HarborStreet.Checkout.Aggregates.Catalog;
using HarborStreet.Checkout.Aggregates.Catalog.Commands;
using HarborStreet.Checkout.Aggregates.Catalog.Events;
using HarborStreet.Checkout.Aggregates.Checkout;
using HarborStreet.Checkout.Aggregates.Orders.Events;
using LawnDart.EventStore;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Checkout.Aggregates.Tests;

/// <summary>
/// Documents last-copy behavior under traditional aggregates + choreography.
/// Source GWT: docs/models/bookstore-v1/LAST_COPY_GWT.md
/// </summary>
public class LastCopyRaceTests
{
    [Fact]
    public async Task happy_path_place_order_reserves_last_copy()
    {
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await SeedLastCopyAsync(ctx, titleId);

        var checkout = new AggregateCheckoutChoreography(ctx.AggregateRepository);
        var outcome = await checkout.PlaceOrderAsync(orderId, customerId, titleId);

        Assert.Equal(CheckoutStatus.Succeeded, outcome.Status);

        var title = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.Equal(0, title.State.AvailableUnits);
        Assert.Equal(1, title.State.ReservedUnits);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
    }

    [Fact]
    public async Task second_customer_after_success_gets_title_unavailable()
    {
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();

        await SeedLastCopyAsync(ctx, titleId);

        var checkout = new AggregateCheckoutChoreography(ctx.AggregateRepository);
        var first = await checkout.PlaceOrderAsync(Guid.NewGuid(), Guid.NewGuid(), titleId);
        var second = await checkout.PlaceOrderAsync(Guid.NewGuid(), Guid.NewGuid(), titleId);

        Assert.Equal(CheckoutStatus.Succeeded, first.Status);
        Assert.Equal(CheckoutStatus.RejectedUnavailable, second.Status);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
    }

// start snippet orphan-order
    [Fact]
    public async Task last_copy_race_under_aggregates_leaves_orphan_order()
    {
        // Given title with 1 available unit
        // And customers A and B both observed that unit (stale reads / skipStockCheck)
        // When both Place Order then Reserve Stock (choreography)
        // Then business GWT fails under this design:
        //   - 2× Order Placed
        //   - 1× Stock Reserved
        //   - one outcome NeedsCompensation (orphan order)
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();

        await SeedLastCopyAsync(ctx, titleId);

        // Both "observe" the last copy before either commits (race model).
        var observedA = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        var observedB = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.Equal(1, observedA.State.AvailableUnits);
        Assert.Equal(1, observedB.State.AvailableUnits);

        var checkout = new AggregateCheckoutChoreography(ctx.AggregateRepository);

        // Interleave without a thread barrier (InMemory serializes on append).
        // skipStockCheck = both already decided based on the observation above.
        var resultA = await checkout.PlaceOrderAsync(
            orderA, Guid.NewGuid(), titleId, skipStockCheck: true);
        var resultB = await checkout.PlaceOrderAsync(
            orderB, Guid.NewGuid(), titleId, skipStockCheck: true);

        var results = new[] { resultA, resultB };
        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        var placed = events.Count(e => e.Event is OrderPlaced);
        var reserved = events.Count(e => e.Event is StockReserved);

        Assert.Equal(2, placed);
        Assert.Equal(1, reserved);
        Assert.Contains(results, r => r.Status == CheckoutStatus.Succeeded);
        Assert.Contains(results, r => r.Status == CheckoutStatus.NeedsCompensation);

        var title = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.Equal(0, title.State.AvailableUnits);
        Assert.Equal(1, title.State.ReservedUnits);

        // Not oversold on Title — the pain is orphan Order Placed without Stock Reserved.
        Assert.True(placed > reserved);
// end snippet orphan-order
    }

    [Fact]
    public async Task compensation_cancels_orphan_order_after_failed_reserve()
    {
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();

        await SeedLastCopyAsync(ctx, titleId);

        var observed = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.Equal(1, observed.State.AvailableUnits);

        var checkout = new AggregateCheckoutChoreography(ctx.AggregateRepository);

        var results = new[]
        {
            await checkout.PlaceOrderAsync(
                Guid.NewGuid(), Guid.NewGuid(), titleId,
                skipStockCheck: true,
                compensateOnReserveFailure: true),
            await checkout.PlaceOrderAsync(
                Guid.NewGuid(), Guid.NewGuid(), titleId,
                skipStockCheck: true,
                compensateOnReserveFailure: true)
        };

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(2, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
        Assert.Equal(1, events.Count(e => e.Event is OrderCancelled));
        Assert.Contains(results, r => r.Status == CheckoutStatus.NeedsCompensation);
    }

    private static async Task SeedLastCopyAsync(BddTestContext ctx, Guid titleId)
    {
        var repo = ctx.AggregateRepository;
        await repo.HandleCommandAsync(
            await repo.GetOrCreateAsync<Title>(titleId),
            new AddTitleToCatalogCommand(
                Guid.NewGuid(), titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true));
        await repo.HandleCommandAsync(
            await repo.GetOrCreateAsync<Title>(titleId),
            new RestockTitleCommand(Guid.NewGuid(), titleId, Quantity: 1));
    }
}
