using HarborStreet.Checkout.Dcb;
using HarborStreet.Checkout.Dcb.Checkout;
using HarborStreet.Checkout.Dcb.Commands;
using HarborStreet.Checkout.Dcb.Events;
using LawnDart.EventStore;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Checkout.Dcb.Tests;

/// <summary>
/// Last-copy GWT under DCB — exactly one winner, no orphan Order Placed.
/// Source: docs/models/bookstore-v1/LAST_COPY_GWT.md
/// Contrast: ch10-aggregates-checkout (orphan order / NeedsCompensation).
/// </summary>
public class LastCopyDcbTests
{
    [Fact]
    public async Task happy_path_place_order_emits_order_and_stock_reserved()
    {
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var checkout = new DcbCheckoutService(ctx.DcbRepository);

        await checkout.SeedLastCopyAsync(titleId);
        var outcome = await checkout.PlaceOrderAsync(orderId, customerId, titleId);

        Assert.Equal(DcbCheckoutStatus.Succeeded, outcome.Status);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced op && op.OrderId == orderId));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved sr && sr.OrderId == orderId));

        var decision = await ctx.DcbRepository.GetOrCreateEntityAsync<CheckoutDecision>(
            CheckoutDecision.TitleTags(titleId));
        Assert.Equal(0, decision.State.AvailableUnits);
        Assert.Equal(1, decision.State.ReservedUnits);
    }

    [Fact]
    public async Task second_customer_after_success_gets_title_unavailable()
    {
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();
        var checkout = new DcbCheckoutService(ctx.DcbRepository);

        await checkout.SeedLastCopyAsync(titleId);
        var first = await checkout.PlaceOrderAsync(Guid.NewGuid(), Guid.NewGuid(), titleId);
        var second = await checkout.PlaceOrderAsync(Guid.NewGuid(), Guid.NewGuid(), titleId);

        Assert.Equal(DcbCheckoutStatus.Succeeded, first.Status);
        Assert.Equal(DcbCheckoutStatus.RejectedUnavailable, second.Status);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
    }

// start snippet concurrent-last-copy
    [Fact]
// start snippet exactly-one-winner
    public async Task concurrent_last_copy_exactly_one_winner()
    {
        // Given title with 1 available unit
        // When A and B Place Order concurrently (same observed decision, race on append)
        // Then exactly one Succeeded; the other ConflictLost or RejectedUnavailable
        // And exactly 1× Order Placed and 1× Stock Reserved (no orphan order)
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();
        var checkout = new DcbCheckoutService(ctx.DcbRepository);

        await checkout.SeedLastCopyAsync(titleId);

        var tags = CheckoutDecision.TitleTags(titleId);
        var entityA = await ctx.DcbRepository.GetOrCreateEntityAsync<CheckoutDecision>(tags);
        var entityB = await ctx.DcbRepository.GetOrCreateEntityAsync<CheckoutDecision>(tags);
        Assert.Equal(1, entityA.State.AvailableUnits);
        Assert.Equal(1, entityB.State.AvailableUnits);

        var results = await Task.WhenAll(
            checkout.PlaceOrderAsync(orderA, Guid.NewGuid(), titleId, preloaded: entityA),
            checkout.PlaceOrderAsync(orderB, Guid.NewGuid(), titleId, preloaded: entityB));

        Assert.Equal(1, results.Count(r => r.Status == DcbCheckoutStatus.Succeeded));
        Assert.Equal(1, results.Count(r =>
            r.Status is DcbCheckoutStatus.ConflictLost or DcbCheckoutStatus.RejectedUnavailable));

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
// end snippet exactly-one-winner

        var winnerOrderId = results.Single(r => r.Status == DcbCheckoutStatus.Succeeded).OrderId;
        Assert.Contains(events, e => e.Event is OrderPlaced op && op.OrderId == winnerOrderId);
        Assert.Contains(events, e => e.Event is StockReserved sr && sr.OrderId == winnerOrderId);

        var decision = await ctx.DcbRepository.GetOrCreateEntityAsync<CheckoutDecision>(tags);
        Assert.Equal(0, decision.State.AvailableUnits);
        Assert.Equal(1, decision.State.ReservedUnits);
// end snippet concurrent-last-copy
    }

    [Fact]
    public async Task dcb_spec_place_order_emits_both_facts()
    {
        await using var ctx = BddTestContext.CreateInMemory(CheckoutEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var tags = CheckoutDecision.TitleTags(titleId);

        await new DcbCheckoutService(ctx.DcbRepository).SeedLastCopyAsync(titleId);

        await DcbSpec
            .For<CheckoutDecision, PlaceOrderCommand>(
                ctx,
                tags,
                new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, titleId, 1))
            .ThenEmittedEvent<OrderPlaced>(e => e.OrderId == orderId && e.TitleId == titleId)
            .ThenEmittedEvent<StockReserved>(e => e.OrderId == orderId && e.Quantity == 1)
            .AndNoConflict()
            .RunAsync();
    }
}
