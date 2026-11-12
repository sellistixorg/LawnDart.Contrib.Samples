using HarborStreet.EndToEnd;
using HarborStreet.EndToEnd.Bookstore;
using HarborStreet.EndToEnd.Checkout;
using HarborStreet.EndToEnd.Domain;
using HarborStreet.EndToEnd.Events;
using HarborStreet.EndToEnd.Payments;
using HarborStreet.EndToEnd.Reactions;
using LawnDart.EventStore;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.EndToEnd.Tests;

/// <summary>
/// Capstone: happy path (place → capture → fulfill) + last-copy race.
/// GWT: docs/models/facts-before-features/bookstore-v1/LAST_COPY_GWT.md and payment follow-on.
/// </summary>
public class EndToEndTests
{
    private static HarborStreetBookstore CreateBookstore(
        BddTestContext ctx,
        FakePaymentGateway? gateway = null)
    {
        var gw = gateway ?? new FakePaymentGateway { NextCaptureSucceeds = true };
        var reactions = new PaymentFollowOnReactions(ctx.AggregateRepository, ctx.DcbRepository);
        var payments = new PaymentCaptureService(ctx.AggregateRepository, gw, reactions);
        return new HarborStreetBookstore(ctx.DcbRepository, ctx.AggregateRepository, payments);
    }

    [Fact]
// start snippet happy-path
    public async Task happy_path_place_capture_fulfill()
    {
        await using var ctx = BddTestContext.CreateInMemory(EndToEndEventTypes.All);
        var bookstore = CreateBookstore(ctx);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await bookstore.SeedLastCopyAsync(titleId);
        var placed = await bookstore.PlaceOrderAsync(orderId, Guid.NewGuid(), titleId);
        Assert.Equal(CheckoutStatus.Succeeded, placed.Status);

        await bookstore.CapturePaymentAsync(orderId, 52.00m);

        var order = await bookstore.GetOrderAsync(orderId);
        Assert.True(order.State.IsPaid);
        Assert.True(order.State.FulfillmentStarted);
        Assert.False(order.State.IsCancelled);

        var inventory = await bookstore.GetInventoryAsync(titleId);
        Assert.Equal(0, inventory.State.AvailableUnits);
        Assert.Equal(1, inventory.State.ReservedUnits);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
        Assert.Equal(1, events.Count(e => e.Event is PaymentCaptured));
        Assert.Equal(1, events.Count(e => e.Event is FulfillmentStarted));
        Assert.Equal(0, events.Count(e => e.Event is StockReleased));
    }
// end snippet happy-path

    [Fact]
    public async Task payment_failed_cancels_and_releases_stock()
    {
        await using var ctx = BddTestContext.CreateInMemory(EndToEndEventTypes.All);
        var bookstore = CreateBookstore(ctx, new FakePaymentGateway { NextCaptureSucceeds = false });
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await bookstore.SeedLastCopyAsync(titleId);
        Assert.Equal(CheckoutStatus.Succeeded,
            (await bookstore.PlaceOrderAsync(orderId, Guid.NewGuid(), titleId)).Status);

        await bookstore.CapturePaymentAsync(orderId, 52.00m);

        var order = await bookstore.GetOrderAsync(orderId);
        Assert.True(order.State.PaymentFailed);
        Assert.True(order.State.IsCancelled);
        Assert.False(order.State.FulfillmentStarted);

        var inventory = await bookstore.GetInventoryAsync(titleId);
        Assert.Equal(1, inventory.State.AvailableUnits);
        Assert.Equal(0, inventory.State.ReservedUnits);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is PaymentFailed));
        Assert.Equal(1, events.Count(e => e.Event is OrderCancelled));
        Assert.Equal(1, events.Count(e => e.Event is StockReleased));
    }

    [Fact]
    public async Task concurrent_last_copy_exactly_one_winner()
    {
        await using var ctx = BddTestContext.CreateInMemory(EndToEndEventTypes.All);
        var bookstore = CreateBookstore(ctx);
        var titleId = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();

        await bookstore.SeedLastCopyAsync(titleId);

        var query = InventoryDecision.ForTitle(titleId);
        var entityA = await ctx.DcbRepository.GetOrCreateEntityAsync<InventoryDecision>(query);
        var entityB = await ctx.DcbRepository.GetOrCreateEntityAsync<InventoryDecision>(query);
        Assert.Equal(1, entityA.State.AvailableUnits);
        Assert.Equal(1, entityB.State.AvailableUnits);

        var results = await Task.WhenAll(
            bookstore.PlaceOrderAsync(orderA, Guid.NewGuid(), titleId, preloaded: entityA),
            bookstore.PlaceOrderAsync(orderB, Guid.NewGuid(), titleId, preloaded: entityB));

        Assert.Equal(1, results.Count(r => r.Status == CheckoutStatus.Succeeded));
        Assert.Equal(1, results.Count(r =>
            r.Status is CheckoutStatus.ConflictLost or CheckoutStatus.RejectedUnavailable));

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        // Scarce fact: one reservation. OrderPlaced appears on DCB + winner's order stream.
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
        var placedOrderIds = events
            .Select(e => e.Event)
            .OfType<OrderPlaced>()
            .Select(op => op.OrderId)
            .Distinct()
            .ToList();
        Assert.Single(placedOrderIds);
        Assert.Equal(results.Single(r => r.Status == CheckoutStatus.Succeeded).OrderId, placedOrderIds[0]);

        var inventory = await bookstore.GetInventoryAsync(titleId);
        Assert.Equal(0, inventory.State.AvailableUnits);
        Assert.Equal(1, inventory.State.ReservedUnits);
    }
}
